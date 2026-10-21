import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { tratarErro } from "../../../lib/api";
import { DIA, DIAS_ESCALA } from "../../../lib/format";
import { useProfissionais } from "../../compartilhado/catalogos";
import { useCriarEscala } from "../hooks/useAdministracao";
import { previaEscala } from "./previaEscala";

const HORA = /^\d{2}:\d{2}(:\d{2})?$/;
const minutos = (h: string) => Number(h.slice(0, 2)) * 60 + Number(h.slice(3, 5));

const esquema = z
  .object({
    profissionalId: z.string().min(1, "Selecione o profissional."),
    vigenciaInicio: z.string().regex(DIA, "Informe o início da vigência."),
    semTermino: z.boolean(),
    vigenciaFim: z.string(),
    dias: z.array(z.object({ atende: z.boolean(), inicio: z.string(), fim: z.string(), semAtendimento: z.boolean() })),
  })
  .superRefine((d, ctx) => {
    if (!d.semTermino) {
      if (!DIA.test(d.vigenciaFim)) ctx.addIssue({ code: "custom", path: ["vigenciaFim"], message: "Informe o fim da vigência ou marque “Sem data de término”." });
      else if (DIA.test(d.vigenciaInicio) && d.vigenciaFim < d.vigenciaInicio) ctx.addIssue({ code: "custom", path: ["vigenciaFim"], message: "O fim da vigência não pode ser anterior ao início." });
    }
    if (!d.dias.some((x) => x.atende)) ctx.addIssue({ code: "custom", path: ["dias"], message: "Marque ao menos um dia de atendimento." });
    d.dias.forEach((x, i) => {
      if (!x.atende) return;
      if (!HORA.test(x.inicio)) ctx.addIssue({ code: "custom", path: ["dias", i, "inicio"], message: "Informe o horário de início." });
      else if (minutos(x.inicio) % 15 !== 0) ctx.addIssue({ code: "custom", path: ["dias", i, "inicio"], message: "Use horários em múltiplos de 15 minutos." });
      if (!HORA.test(x.fim)) ctx.addIssue({ code: "custom", path: ["dias", i, "fim"], message: "Informe o horário de fim." });
      else if (minutos(x.fim) % 15 !== 0) ctx.addIssue({ code: "custom", path: ["dias", i, "fim"], message: "Use horários em múltiplos de 15 minutos." });
      else if (HORA.test(x.inicio) && minutos(x.fim) <= minutos(x.inicio)) ctx.addIssue({ code: "custom", path: ["dias", i, "fim"], message: "O fim deve ser depois do início." });
    });
  });
type Dados = z.infer<typeof esquema>;
const CAMPOS = ["profissionalId", "vigenciaInicio", "vigenciaFim"] as const;

// Parte do padrão do sistema: segunda a sexta, 07h às 17h, sexta sinalizada como sem atendimento.
const PADRAO_DIAS = DIAS_ESCALA.map(({ dia }) => {
  const util = dia !== "Saturday" && dia !== "Sunday";
  return { atende: util, inicio: util ? "07:00" : "", fim: util ? "17:00" : "", semAtendimento: dia === "Friday" };
});

export function EscalaForm({ aoConcluir, aoCancelar }: { aoConcluir: () => void; aoCancelar: () => void }) {
  // Uma chave por formulário: retentativas reaproveitam a mesma criação.
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const profissionais = useProfissionais();
  const criar = useCriarEscala();
  const {
    register,
    handleSubmit,
    setError,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Dados>({ resolver: zodResolver(esquema), defaultValues: { profissionalId: "", vigenciaInicio: "", semTermino: true, vigenciaFim: "", dias: PADRAO_DIAS } });
  const semTermino = watch("semTermino");
  const dias = watch("dias");
  const previa = previaEscala(DIAS_ESCALA.flatMap(({ dia }, i) => (dias[i]?.atende && dias[i].inicio && dias[i].fim ? [{ dia, inicio: dias[i].inicio, fim: dias[i].fim, semAtendimento: dias[i].semAtendimento }] : [])));

  async function enviar(d: Dados) {
    setErroGeral(null);
    try {
      await criar.mutateAsync({
        chave,
        body: {
          profissionalId: d.profissionalId,
          vigenciaInicio: d.vigenciaInicio,
          vigenciaFim: d.semTermino ? null : d.vigenciaFim,
          faixas: DIAS_ESCALA.flatMap(({ dia }, i) => {
            const x = d.dias[i]!;
            return x.atende ? [{ dia, inicio: `${x.inicio.slice(0, 5)}:00`, fim: `${x.fim.slice(0, 5)}:00`, semAtendimento: x.semAtendimento }] : [];
          }),
        },
      });
      aoConcluir();
    } catch (e) {
      // 409 (vigência sobreposta a outra escala) e demais falhas vão para o Alert, mantendo o formulário.
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente">{erroGeral}</Alert>}
      {profissionais.isError && <Alert variante="urgente">Não foi possível carregar os profissionais. Feche e tente novamente.</Alert>}
      <Field rotulo="Profissional" erro={errors.profissionalId?.message} obrigatorio>
        <Select {...register("profissionalId")}>
          <option value="">{profissionais.isPending ? "Carregando…" : "Selecione"}</option>
          {profissionais.data?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nome}
            </option>
          ))}
        </Select>
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Início da vigência" erro={errors.vigenciaInicio?.message} obrigatorio>
          <Input type="date" {...register("vigenciaInicio")} />
        </Field>
        <Field rotulo="Fim da vigência" erro={errors.vigenciaFim?.message}>
          <Input type="date" disabled={semTermino} {...register("vigenciaFim")} />
        </Field>
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" className="size-4" {...register("semTermino")} />
        Sem data de término
      </label>

      <fieldset className="flex flex-col gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-medium">Dias de atendimento</legend>
        {DIAS_ESCALA.map(({ dia, nome }, i) => {
          const atende = dias[i]?.atende;
          const erroLinha = errors.dias?.[i]?.inicio?.message ?? errors.dias?.[i]?.fim?.message;
          return (
            <div key={dia} className="flex flex-col gap-1 border-b border-borda pb-2 last:border-0">
              <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
                <label className="flex w-40 items-center gap-2 text-sm font-medium">
                  <input type="checkbox" className="size-4" {...register(`dias.${i}.atende`)} />
                  {nome}
                </label>
                <Input type="time" step={900} aria-label={`Início de ${nome}`} disabled={!atende} invalid={!!errors.dias?.[i]?.inicio} className="w-32" {...register(`dias.${i}.inicio`)} />
                <Input type="time" step={900} aria-label={`Fim de ${nome}`} disabled={!atende} invalid={!!errors.dias?.[i]?.fim} className="w-32" {...register(`dias.${i}.fim`)} />
                <label className="flex items-center gap-2 text-denso text-suave">
                  <input type="checkbox" className="size-4" disabled={!atende} {...register(`dias.${i}.semAtendimento`)} />
                  Sem atendimento
                </label>
              </div>
              {erroLinha && (
                <p role="alert" className="text-xs text-urgente">
                  {nome}: {erroLinha}
                </p>
              )}
            </div>
          );
        })}
        {errors.dias?.message && (
          <p role="alert" className="text-xs text-urgente">
            {errors.dias.message}
          </p>
        )}
        <p className="text-xs text-suave">“Sem atendimento” só sinaliza o dia na agenda; não bloqueia agendamentos.</p>
      </fieldset>

      <p className="m-0 rounded-md bg-linha px-3 py-2 text-denso">
        <span className="font-semibold">Prévia: </span>
        {previa}
      </p>
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Cancelar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Salvar
        </Button>
      </div>
    </form>
  );
}
