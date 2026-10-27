import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm, type UseFormRegisterReturn } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Dado } from "../../../components/ui/Dado";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { diaCivil, formatarData, formatarIntervalo, hojeCivil, HORA, horaCivil, instanteCivil } from "../../../lib/format";
import type { Compromisso } from "../../../lib/types";
import { CAMPO_DA_API } from "../../agenda/components/CompromissoForm";
import { useCursos, useProfissionais } from "../../compartilhado/catalogos";
import { useRegistrarAtividade } from "../hooks/useAtividades";
import { entre } from "../../../lib/entre";
import { ROTULO_TIPO_ATIVIDADE, TIPOS_ATIVIDADE } from "../rotulos";
import type { RegistrarAtividadeBody } from "../services/atividadeService";

type CampoComum = "data" | "inicio" | "fim" | "cursoId" | "tipo" | "descricao";
type ValoresComuns = Record<CampoComum, string>;

/** Regras de formato e de horário comuns ao registro e à correção; o resto (escopo, conflito) é da API. */
export function verificarAtividade(v: ValoresComuns, ctx: z.RefinementCtx) {
  const erro = (path: CampoComum, message: string) => ctx.addIssue({ code: "custom", path: [path], message });
  if (!entre(TIPOS_ATIVIDADE, v.tipo)) erro("tipo", "Selecione o tipo.");
  const descricao = v.descricao.trim();
  if (descricao.length < 3 || descricao.length > 2000) erro("descricao", "Descreva a atividade (de 3 a 2000 caracteres).");
  const dataValida = z.iso.date().safeParse(v.data).success;
  if (!dataValida) erro("data", "Informe a data.");
  if (!HORA.test(v.inicio)) erro("inicio", "Informe o horário de início.");
  if (!HORA.test(v.fim)) erro("fim", "Informe o horário de término.");
  if (!HORA.test(v.inicio) || !HORA.test(v.fim)) return;
  if (v.fim <= v.inicio) erro("fim", "O horário de término deve ser posterior ao de início.");
  else if (dataValida && Date.parse(instanteCivil(v.data, v.fim)) > Date.now()) erro("fim", "A atividade ainda não terminou; registre depois do horário de término.");
}

/** Data, horário (de 15 em 15 minutos), curso, tipo e descrição. `cursoAtual` mantém na lista um curso já inativo. */
export function CamposAtividade({
  register: r,
  errors,
  cursoAtual,
}: {
  register: (nome: CampoComum) => UseFormRegisterReturn;
  errors: Partial<Record<CampoComum, { message?: string }>>;
  cursoAtual?: { id: string; nome: string };
}) {
  const cursos = useCursos();
  const opcoes = (cursos.data ?? []).filter((c) => c.ativo || c.id === cursoAtual?.id).map((c) => ({ id: c.id, nome: c.nome }));
  if (cursoAtual && !opcoes.some((c) => c.id === cursoAtual.id)) opcoes.push(cursoAtual);
  return (
    <>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field rotulo="Data" erro={errors.data?.message} obrigatorio>
          <Input type="date" max={hojeCivil()} {...r("data")} />
        </Field>
        <Field rotulo="Início" erro={errors.inicio?.message} obrigatorio>
          <Input type="time" step={900} {...r("inicio")} />
        </Field>
        <Field rotulo="Fim" erro={errors.fim?.message} obrigatorio>
          <Input type="time" step={900} {...r("fim")} />
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Curso" erro={errors.cursoId?.message}>
          <Select {...r("cursoId")}>
            <option value="">Institucional (sem curso)</option>
            {opcoes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field rotulo="Tipo" erro={errors.tipo?.message} obrigatorio>
          <Select {...r("tipo")}>
            <option value="">Selecione</option>
            {TIPOS_ATIVIDADE.map((t) => (
              <option key={t} value={t}>
                {ROTULO_TIPO_ATIVIDADE[t]}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <Field rotulo="Descrição" erro={errors.descricao?.message} obrigatorio>
        <Textarea {...r("descricao")} />
      </Field>
    </>
  );
}

const esquema = z
  .object({
    avulsa: z.boolean(),
    data: z.string(),
    inicio: z.string(),
    fim: z.string(),
    cursoId: z.string(),
    tipo: z.string(),
    descricao: z.string(),
    profissionalId: z.string(),
    justificativaAvulso: z.string().trim(),
  })
  .superRefine((v, ctx) => {
    verificarAtividade(v, ctx);
    if (v.avulsa && (v.justificativaAvulso.length < 5 || v.justificativaAvulso.length > 500))
      ctx.addIssue({ code: "custom", path: ["justificativaAvulso"], message: "Informe a justificativa (de 5 a 500 caracteres)." });
  });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS: (keyof Entrada)[] = ["data", "inicio", "fim", "cursoId", "tipo", "descricao", "profissionalId", "justificativaAvulso"];

export function AtividadeForm({
  compromisso,
  coordenacao = false,
  aoConcluir,
  aoCancelar,
}: {
  /** Registro a partir do agendamento: o horário vem do compromisso e pode ser ajustado. */
  compromisso?: Compromisso;
  /** Só a coordenação escolhe o profissional de uma atividade avulsa. */
  coordenacao?: boolean;
  aoConcluir: (id: string) => void;
  aoCancelar?: () => void;
}) {
  const avulsa = !compromisso;
  // Uma chave por formulário: retentativas reaproveitam a mesma.
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const registrar = useRegistrarAtividade();
  const profissionais = useProfissionais(avulsa && coordenacao);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: {
      avulsa,
      data: compromisso ? diaCivil(compromisso.inicio) : hojeCivil(),
      inicio: compromisso ? horaCivil(compromisso.inicio) : "",
      fim: compromisso ? horaCivil(compromisso.fim) : "",
      cursoId: "",
      tipo: "",
      descricao: "",
      profissionalId: "",
      justificativaAvulso: "",
    },
  });

  async function enviar(d: Saida) {
    setErroGeral(null);
    const tipo = entre(TIPOS_ATIVIDADE, d.tipo);
    if (!tipo) return;
    const body: RegistrarAtividadeBody = {
      compromissoId: compromisso?.id ?? null,
      profissionalId: avulsa && coordenacao && d.profissionalId ? d.profissionalId : null,
      inicio: instanteCivil(d.data, d.inicio),
      fim: instanteCivil(d.data, d.fim),
      cursoId: d.cursoId || null,
      tipo,
      descricao: d.descricao.trim(),
      justificativaAvulso: avulsa ? d.justificativaAvulso : null,
    };
    try {
      const { id } = await registrar.mutateAsync({ body, chave });
      aoConcluir(id);
    } catch (e) {
      // 409 (conflito de horário, compromisso já registrado) e 422 vão para o Alert; o formulário mantém o digitado.
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message }), CAMPO_DA_API));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      {compromisso && (
        <dl className="grid grid-cols-1 gap-3 rounded-md bg-realce px-3 py-2.5 text-sm sm:grid-cols-2">
          <Dado rotulo="Horário agendado">
            {formatarData(compromisso.inicio)}, {formatarIntervalo(compromisso.inicio, compromisso.fim)}
          </Dado>
          <Dado rotulo="Profissional">{compromisso.profissional}</Dado>
        </dl>
      )}
      <CamposAtividade register={register} errors={errors} />
      {avulsa && coordenacao && (
        <Field rotulo="Profissional" erro={errors.profissionalId?.message}>
          <Select {...register("profissionalId")}>
            <option value="">Você</option>
            {profissionais.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nome}
              </option>
            ))}
          </Select>
        </Field>
      )}
      {avulsa && (
        <Field rotulo="Justificativa da atividade avulsa" erro={errors.justificativaAvulso?.message} dica="Por que não houve agendamento (de 5 a 500 caracteres)." obrigatorio>
          <Textarea {...register("justificativaAvulso")} />
        </Field>
      )}
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
