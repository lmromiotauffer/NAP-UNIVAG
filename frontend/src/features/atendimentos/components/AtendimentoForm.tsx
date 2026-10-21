import { zodResolver } from "@hookform/resolvers/zod";
import { useId, useState } from "react";
import { useForm, type UseFormRegisterReturn } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { formatarData, formatarHora, hojeCivil, instanteCivil } from "../../../lib/format";
import type { Compromisso } from "../../../lib/types";
import { camposHorario, CAMPO_DA_API, ListasHorario } from "../../agenda/components/CompromissoForm";
import { estiloAtendimento, ROTULO_TIPO_ATENDIMENTO } from "../../agenda/components/estiloCompromisso";
import { BuscaAluno, type AlunoEscolhido } from "../../alunos/components/BuscaAluno";
import { useRegistrarAtendimento } from "../hooks/useAtendimentos";
import type { RegistrarAtendimentoBody } from "../services/atendimentoService";

type TipoAtendimento = RegistrarAtendimentoBody["tipo"];
type EstadoAtendimento = RegistrarAtendimentoBody["estado"];
export const TIPOS_ATENDIMENTO = Object.keys(ROTULO_TIPO_ATENDIMENTO) as [TipoAtendimento, ...TipoAtendimento[]];

export const textoClinico = z
  .string()
  .trim()
  .max(5000, "Use no máximo 5000 caracteres.")
  .transform((v) => (v === "" ? null : v));

const horarioAvulso = z.object(camposHorario);
export const ehTipo = (t: string): t is TipoAtendimento => (TIPOS_ATENDIMENTO as string[]).includes(t);

const esquema = z
  .object({
    avulso: z.boolean(),
    estado: z.enum(["Realizado", "Ausente"], "Selecione o estado."),
    // String + checagem no superRefine: um enum inválido abortaria o refine e esconderia o erro da demanda.
    tipo: z.string(),
    alunoId: z.string(),
    data: z.string(),
    inicio: z.string(),
    fim: z.string(),
    justificativaAvulso: z.string().trim(),
    demandaPrincipal: textoClinico,
    observacoesPrivadas: textoClinico,
    recomendacoesEncaminhamento: textoClinico,
    proximosPassos: textoClinico,
  })
  .superRefine((v, ctx) => {
    if (!ehTipo(v.tipo)) ctx.addIssue({ code: "custom", path: ["tipo"], message: "Selecione o tipo." });
    if (v.estado === "Realizado" && v.demandaPrincipal == null)
      ctx.addIssue({ code: "custom", path: ["demandaPrincipal"], message: "Informe a demanda principal." });
    if (!v.avulso) return;
    if (!v.alunoId) ctx.addIssue({ code: "custom", path: ["alunoId"], message: "Selecione o aluno." });
    const horario = horarioAvulso.safeParse(v);
    if (!horario.success) for (const issue of horario.error.issues) ctx.addIssue({ code: "custom", path: issue.path, message: issue.message });
    else if (v.fim <= v.inicio) ctx.addIssue({ code: "custom", path: ["fim"], message: "O horário de término deve ser posterior ao de início." });
    else if (Date.parse(instanteCivil(v.data, v.fim)) > Date.now())
      ctx.addIssue({ code: "custom", path: ["fim"], message: "O atendimento ainda não terminou; registre depois do horário de término." });
    if (v.justificativaAvulso.length < 5 || v.justificativaAvulso.length > 500)
      ctx.addIssue({ code: "custom", path: ["justificativaAvulso"], message: "Informe a justificativa (de 5 a 500 caracteres)." });
  });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS_CLINICOS = ["demandaPrincipal", "observacoesPrivadas", "recomendacoesEncaminhamento", "proximosPassos"] as const;
type CampoClinico = (typeof CAMPOS_CLINICOS)[number];
const CAMPOS_AVULSO = ["alunoId", "data", "fim", "justificativaAvulso"] as const;

const NOTA_AUSENCIA = "Ausência não exige demanda ou nota clínica.";
export const ROTULO_PRIVADAS = "Visível apenas ao NAP";

/** Campos clínicos do registro e da correção. Ocultos (e não enviados) quando o aluno faltou. */
export function CamposClinicos({ register: r, errors, demandaObrigatoria = true }: { register: (nome: CampoClinico) => UseFormRegisterReturn; errors: Partial<Record<CampoClinico, { message?: string }>>; demandaObrigatoria?: boolean }) {
  return (
    <>
      <Field rotulo="Demanda principal" erro={errors.demandaPrincipal?.message} obrigatorio={demandaObrigatoria}>
        <Textarea {...r("demandaPrincipal")} />
      </Field>
      <Field rotulo="Observações privadas" erro={errors.observacoesPrivadas?.message} dica={`${ROTULO_PRIVADAS}. Não aparece em listas, na ficha nem no painel.`}>
        <Textarea {...r("observacoesPrivadas")} />
      </Field>
      <Field rotulo="Recomendações e encaminhamentos" erro={errors.recomendacoesEncaminhamento?.message}>
        <Textarea {...r("recomendacoesEncaminhamento")} />
      </Field>
      <Field rotulo="Próximos passos" erro={errors.proximosPassos?.message}>
        <Textarea {...r("proximosPassos")} />
      </Field>
    </>
  );
}

/** Estado como rádio (texto, não cor): Realizado ou Ausente no registro; os quatro na correção. */
export function EscolhaEstado({ opcoes, registro, erro }: { opcoes: readonly EstadoAtendimento[]; registro: UseFormRegisterReturn; erro?: string }) {
  const id = useId();
  return (
    <fieldset className="flex flex-col gap-1" aria-describedby={erro ? `${id}-erro` : undefined}>
      <legend className="mb-1 text-sm font-medium text-texto">
        Estado<span className="ml-0.5 text-urgente" aria-hidden="true">*</span>
      </legend>
      <div className="flex flex-wrap gap-2">
        {opcoes.map((o) => (
          <label key={o} className="flex cursor-pointer items-center gap-2 rounded-md border border-borda px-3 py-2 text-sm has-checked:border-primaria has-checked:bg-realce">
            <input type="radio" value={o} className="accent-primaria" {...registro} />
            {estiloAtendimento(o).rotuloEstado}
          </label>
        ))}
      </div>
      {erro && (
        <p id={`${id}-erro`} role="alert" className="text-xs text-urgente">
          {erro}
        </p>
      )}
    </fieldset>
  );
}

export function AtendimentoForm({
  compromisso,
  aluno: alunoInicial,
  aoConcluir,
  aoCancelar,
}: {
  /** Registro a partir do agendamento: a API toma aluno, profissional e horário do compromisso. */
  compromisso?: Compromisso;
  /** Avulso já com o aluno escolhido (ex.: aberto da ficha). */
  aluno?: AlunoEscolhido;
  aoConcluir: (id: string) => void;
  aoCancelar?: () => void;
}) {
  const avulso = !compromisso;
  // Uma chave por formulário: retentativas reaproveitam a mesma.
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const [aluno, setAluno] = useState<AlunoEscolhido | null>(alunoInicial ?? null);
  const registrar = useRegistrarAtendimento();
  const idHorarios = useId();
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: {
      avulso,
      estado: "Realizado",
      tipo: "",
      alunoId: alunoInicial?.id ?? "",
      data: hojeCivil(),
      inicio: "",
      fim: "",
      justificativaAvulso: "",
      demandaPrincipal: "",
      observacoesPrivadas: "",
      recomendacoesEncaminhamento: "",
      proximosPassos: "",
    },
  });
  const ausente = watch("estado") === "Ausente";

  function escolherAluno(a: AlunoEscolhido | null) {
    setAluno(a);
    setValue("alunoId", a?.id ?? "", { shouldValidate: !!a });
  }

  async function enviar(d: Saida) {
    setErroGeral(null);
    if (!ehTipo(d.tipo)) return;
    const tipo = d.tipo;
    // Ausência: os campos clínicos digitados antes de trocar o estado ficam no formulário, mas não saem dele.
    const realizado = d.estado === "Realizado";
    const clinicos = {
      demandaPrincipal: realizado ? d.demandaPrincipal : null,
      observacoesPrivadas: realizado ? d.observacoesPrivadas : null,
      recomendacoesEncaminhamento: realizado ? d.recomendacoesEncaminhamento : null,
      proximosPassos: realizado ? d.proximosPassos : null,
    };
    const body: RegistrarAtendimentoBody = compromisso
      ? { compromissoId: compromisso.id, alunoId: null, profissionalId: null, inicio: null, fim: null, tipo, estado: d.estado, ...clinicos, justificativaAvulso: null }
      : {
          compromissoId: null,
          alunoId: d.alunoId,
          profissionalId: null,
          inicio: instanteCivil(d.data, d.inicio),
          fim: instanteCivil(d.data, d.fim),
          tipo,
          estado: d.estado,
          ...clinicos,
          justificativaAvulso: d.justificativaAvulso,
        };
    try {
      const { id } = await registrar.mutateAsync({ body, chave });
      aoConcluir(id);
    } catch (e) {
      const visiveis = ["tipo", "estado", ...(realizado ? CAMPOS_CLINICOS : []), ...(avulso ? CAMPOS_AVULSO : [])] as (keyof Entrada)[];
      setErroGeral(tratarErro(e, visiveis, (campo, message) => setError(campo, { message }), avulso ? CAMPO_DA_API : {}));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente">{erroGeral}</Alert>}
      {compromisso ? (
        <dl className="grid grid-cols-1 gap-3 rounded-md bg-realce px-3 py-2.5 text-sm sm:grid-cols-3">
          <div>
            <dt className="text-xs text-suave">Aluno</dt>
            <dd className="m-0 font-semibold">{compromisso.aluno ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-xs text-suave">Horário agendado</dt>
            <dd className="m-0">
              {formatarData(compromisso.inicio)}, {formatarHora(compromisso.inicio)}–{formatarHora(compromisso.fim)}
            </dd>
          </div>
          <div>
            <dt className="text-xs text-suave">Profissional</dt>
            <dd className="m-0">{compromisso.profissional}</dd>
          </div>
        </dl>
      ) : (
        <>
          <BuscaAluno aluno={aluno} aoEscolher={escolherAluno} erro={errors.alunoId?.message} obrigatorio />
          <div className="grid gap-4 sm:grid-cols-3">
            <Field rotulo="Data" erro={errors.data?.message} dica="Segunda a sexta-feira." obrigatorio>
              <Input type="date" max={hojeCivil()} {...register("data")} />
            </Field>
            <Field rotulo="Início" erro={errors.inicio?.message} obrigatorio>
              <Input inputMode="numeric" placeholder="00:00" list={`${idHorarios}-inicio`} autoComplete="off" {...register("inicio")} />
            </Field>
            <Field rotulo="Fim" erro={errors.fim?.message} obrigatorio>
              <Input inputMode="numeric" placeholder="00:00" list={`${idHorarios}-fim`} autoComplete="off" {...register("fim")} />
            </Field>
            <ListasHorario id={idHorarios} />
          </div>
          <Field rotulo="Justificativa do atendimento avulso" erro={errors.justificativaAvulso?.message} dica="Por que não houve agendamento (de 5 a 500 caracteres)." obrigatorio>
            <Textarea {...register("justificativaAvulso")} />
          </Field>
        </>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <EscolhaEstado opcoes={["Realizado", "Ausente"] as const} registro={register("estado")} erro={errors.estado?.message} />
        <Field rotulo="Tipo" erro={errors.tipo?.message} dica={watch("tipo") === "OrientacaoAcademica" ? "Registro interno do NAP; não gera retorno de outro setor" : undefined} obrigatorio>
          <Select {...register("tipo")}>
            <option value="">Selecione</option>
            {TIPOS_ATENDIMENTO.map((t) => (
              <option key={t} value={t}>
                {ROTULO_TIPO_ATENDIMENTO[t]}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      {ausente ? <p className="m-0 rounded-md bg-realce px-3 py-2.5 text-sm text-suave">{NOTA_AUSENCIA}</p> : <CamposClinicos register={register} errors={errors} />}
      <div className="flex flex-wrap justify-end gap-2">
        {aoCancelar && (
          <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
            Cancelar
          </Button>
        )}
        <Button type="submit" carregando={isSubmitting}>
          Registrar
        </Button>
      </div>
    </form>
  );
}
