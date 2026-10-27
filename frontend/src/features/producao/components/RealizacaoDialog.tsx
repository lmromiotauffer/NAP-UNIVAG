import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useId, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { diaCivil, formatarData, formatarIntervalo, horaCivil, instanteCivil } from "../../../lib/format";
import type { Compromisso } from "../../../lib/types";
import { CAMPO_DA_API, camposHorario, ListasHorario, validarHorario } from "../../agenda/components/CompromissoForm";
import { useCorrigirRealizacao, useRegistrarRealizacao } from "../hooks/useProducao";

// A justificativa só é exigida na correção.
const esquemaDe = (correcao: boolean) =>
  z.object({ ...camposHorario, justificativa: z.string().trim().max(500, "Use no máximo 500 caracteres.") }).superRefine((v, ctx) => {
    validarHorario(v, ctx);
    if (correcao && v.justificativa.length < 5) ctx.addIssue({ code: "custom", path: ["justificativa"], message: "Informe a justificativa (mínimo 5 caracteres)." });
  });
type Entrada = z.input<ReturnType<typeof esquemaDe>>;
type Saida = z.output<ReturnType<typeof esquemaDe>>;
const CAMPOS = [...Object.keys(camposHorario), "justificativa"] as (keyof Entrada)[];

/**
 * Formulário de realização (vai dentro de um Modal). Início e fim partem do horário exibido (o agendado; na correção, o efetivo);
 * a versão é a do compromisso ao abrir. Com `correcao`, corrige a realização já registrada e exige justificativa.
 */
export function RealizacaoDialog({
  compromisso,
  correcao = false,
  aoConcluir,
  aoCancelar,
}: {
  compromisso: Compromisso;
  correcao?: boolean;
  aoConcluir: () => void;
  aoCancelar: () => void;
}) {
  // Snapshot na abertura: uma agenda que se atualize por baixo não troca a versão enviada.
  const [versao] = useState(compromisso.versao);
  const [esquema] = useState(() => esquemaDe(correcao));
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const registrar = useRegistrarRealizacao();
  const corrigir = useCorrigirRealizacao();
  const idHorarios = useId();
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: { data: diaCivil(compromisso.inicio), inicio: horaCivil(compromisso.inicio), fim: horaCivil(compromisso.fim), justificativa: "" },
  });

  useEffect(() => setFocus("data"), [setFocus]);

  async function enviar(d: Saida) {
    setErroGeral(null);
    try {
      const horario = { inicio: instanteCivil(d.data, d.inicio), fim: instanteCivil(d.data, d.fim) };
      if (correcao) await corrigir.mutateAsync({ id: compromisso.id, body: { ...horario, justificativa: d.justificativa }, versao });
      else await registrar.mutateAsync({ id: compromisso.id, body: horario, versao });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message }), CAMPO_DA_API));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      <p className="text-sm text-suave">
        {correcao ? "Registrado" : "Agendado"}: {formatarData(compromisso.inicio)}, {formatarIntervalo(compromisso.inicio, compromisso.fim)}. Informe o horário em que de fato aconteceu; é ele que conta na produção e ocupa a agenda.
      </p>
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      <div className="grid gap-4 sm:grid-cols-3">
        <Field rotulo="Data" erro={errors.data?.message} obrigatorio>
          <Input type="date" {...register("data")} />
        </Field>
        <Field rotulo="Início" erro={errors.inicio?.message} obrigatorio>
          <Input inputMode="numeric" placeholder="00:00" list={`${idHorarios}-inicio`} autoComplete="off" {...register("inicio")} />
        </Field>
        <Field rotulo="Fim" erro={errors.fim?.message} obrigatorio>
          <Input inputMode="numeric" placeholder="00:00" list={`${idHorarios}-fim`} autoComplete="off" {...register("fim")} />
        </Field>
        <ListasHorario id={idHorarios} />
      </div>
      {correcao && (
        <Field rotulo="Justificativa da correção" erro={errors.justificativa?.message} obrigatorio>
          <Textarea maxLength={500} {...register("justificativa")} />
        </Field>
      )}
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Voltar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          {correcao ? "Corrigir realização" : "Registrar realização"}
        </Button>
      </div>
    </form>
  );
}
