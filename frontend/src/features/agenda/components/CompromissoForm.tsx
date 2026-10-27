import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useId, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Dado } from "../../../components/ui/Dado";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { normalizarErro, tratarErro } from "../../../lib/api";
import { formatarData, formatarIntervalo, HORA, instanteCivil, minutosDoDia } from "../../../lib/format";
import type { Compromisso, ConflitoAgenda } from "../../../lib/types";
import { BuscaAluno, type AlunoEscolhido } from "../../alunos/components/BuscaAluno";
import { useProfissionais } from "../../compartilhado/catalogos";
import { useAtualizarCompromisso, useCriarCompromisso } from "../hooks/useAgenda";
import type { EscopoGrupo } from "../services/agendaService";
import { ROTULO_TIPO } from "./estiloCompromisso";
import { ListaConflitos } from "./ListaConflitos";

// Só sugestões de horário: escala, feriado e expediente quem valida é o servidor (422), pois variam por profissional.
const ABERTURA = "07:00";
const ULTIMO_INICIO = "16:45";
const FECHAMENTO = "17:00";
const TIPOS = Object.keys(ROTULO_TIPO) as [Compromisso["tipo"], ...Compromisso["tipo"][]];
const SEM_ALUNO: Compromisso["tipo"][] = ["Bloqueio", "Lembrete"];
// Tipos que aceitam vários participantes (a API recusa `participantesIds` nos demais).
const COM_GRUPO: Compromisso["tipo"][] = ["ReuniaoEquipe", "ReuniaoCoordenacaoGestao", "Lembrete"];

const textoOpcional = z
  .string()
  .trim()
  .max(2000, "Use no máximo 2000 caracteres.")
  .transform((v) => (v === "" ? null : v));

/** Data e horário, compartilhados com a remarcação. */
export const camposHorario = {
  data: z.iso.date("Informe a data."),
  inicio: z.string().trim().regex(HORA, "Use o formato 00:00."),
  fim: z.string().trim().regex(HORA, "Use o formato 00:00."),
};

export function validarHorario(v: { inicio: string; fim: string }, ctx: z.RefinementCtx) {
  if (HORA.test(v.inicio) && HORA.test(v.fim) && v.fim <= v.inicio)
    ctx.addIssue({ code: "custom", path: ["fim"], message: "O horário de término deve ser posterior ao de início." });
}

const esquema = z
  .object({
    tipo: z.enum(TIPOS, "Selecione o tipo."),
    titulo: z.string().trim().min(1, "Informe o título.").max(120, "Use no máximo 120 caracteres."),
    ...camposHorario,
    profissionalId: z.string(),
    alunoId: z.string(),
    descricao: textoOpcional,
  })
  .superRefine((v, ctx) => {
    validarHorario(v, ctx);
    if (v.tipo === "Atendimento" && !v.alunoId) ctx.addIssue({ code: "custom", path: ["alunoId"], message: "Selecione o aluno do atendimento." });
  });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS = Object.keys(esquema.shape) as (keyof Entrada)[];
// Escala e feriado vêm como 422 de domínio, sem `errors.inicio`: o erro cai no `erroGeral` do formulário, não no campo `data`.
// O mapa só vale se a API passar a devolver erro de campo `inicio`, que aqui é a data.
export const CAMPO_DA_API = { inicio: "data" } as const;

/** Horários de 15 em 15 minutos, sugeridos no campo (que também aceita digitação). */
function horarios(de: string, ate: string): string[] {
  const saida: string[] = [];
  for (let m = minutosDoDia(de); m <= minutosDoDia(ate); m += 15)
    saida.push(`${String(Math.floor(m / 60)).padStart(2, "0")}:${String(m % 60).padStart(2, "0")}`);
  return saida;
}

/** Sugestões de início (07:00–16:45) e fim (07:15–17:00) para os campos com `list`. */
export function ListasHorario({ id }: { id: string }) {
  return (
    <>
      <datalist id={`${id}-inicio`}>
        {horarios(ABERTURA, ULTIMO_INICIO).map((h) => (
          <option key={h} value={h} />
        ))}
      </datalist>
      <datalist id={`${id}-fim`}>
        {horarios(ABERTURA, FECHAMENTO)
          .slice(1)
          .map((h) => (
            <option key={h} value={h} />
          ))}
      </datalist>
    </>
  );
}

export function CompromissoForm({
  coordenacao,
  usuarioId,
  profissionalPadrao,
  dataPadrao,
  alunoInicial,
  aoConcluir,
  aoCancelar,
}: {
  /** Coordenação escolhe o profissional; o profissional NAP agenda só para si (a API usa o usuário atual). */
  coordenacao: boolean;
  /** Usuário atual: já vem marcado nos participantes e, fora da coordenação, não pode sair da lista. */
  usuarioId?: string;
  profissionalPadrao?: string;
  dataPadrao?: string;
  alunoInicial?: AlunoEscolhido;
  /** `profissionalId` é o enviado à API: null quando ela agenda para o usuário atual. */
  aoConcluir: (id: string, inicio: string, profissionalId: string | null) => void;
  aoCancelar?: () => void;
}) {
  // Uma chave por formulário: retentativas (ex.: após um 409) reaproveitam a mesma.
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const [conflitos, setConflitos] = useState<ConflitoAgenda[]>([]);
  const [aluno, setAluno] = useState<AlunoEscolhido | null>(alunoInicial ?? null);
  const [participantes, setParticipantes] = useState<string[]>(usuarioId && !coordenacao ? [usuarioId] : []);
  const [todaEquipe, setTodaEquipe] = useState(false);
  const profissionais = useProfissionais();
  const criar = useCriarCompromisso();
  const idHorarios = useId();
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    getValues,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: {
      tipo: "Atendimento",
      titulo: alunoInicial ? "Atendimento" : "",
      data: dataPadrao ?? "",
      inicio: "",
      fim: "",
      profissionalId: coordenacao ? (profissionalPadrao ?? "") : "",
      alunoId: alunoInicial?.id ?? "",
      descricao: "",
    },
  });
  const tipo = watch("tipo");
  const listaProfissionais = profissionais.data;

  // O select só aparece com a lista carregada; um padrão fora dela (ex.: inativo) vira o primeiro da lista,
  // para o valor enviado ser sempre o exibido.
  useEffect(() => {
    if (!coordenacao || !listaProfissionais?.length) return;
    if (!listaProfissionais.some((p) => p.id === getValues("profissionalId"))) setValue("profissionalId", listaProfissionais[0]?.id ?? "");
  }, [coordenacao, listaProfissionais, getValues, setValue]);
  const semAluno = SEM_ALUNO.includes(tipo);
  const comGrupo = COM_GRUPO.includes(tipo);
  // Sozinho (fora da coordenação) é compromisso individual; só vira grupo com mais gente, a equipe toda ou a coordenação escolhendo.
  const emGrupo = comGrupo && (todaEquipe || participantes.length > (coordenacao ? 0 : 1));

  function alternarParticipante(id: string) {
    setParticipantes((atual) => (atual.includes(id) ? atual.filter((p) => p !== id) : [...atual, id]));
  }

  function escolherAluno(a: AlunoEscolhido | null) {
    setAluno(a);
    setValue("alunoId", a?.id ?? "", { shouldValidate: !!a });
  }

  async function enviar(d: Saida) {
    setErroGeral(null);
    setConflitos([]);
    const inicio = instanteCivil(d.data, d.inicio);
    const grupo = COM_GRUPO.includes(d.tipo) && emGrupo;
    const profissionalId = !grupo && coordenacao && d.profissionalId ? d.profissionalId : null;
    try {
      const { id } = await criar.mutateAsync({
        body: {
          tipo: d.tipo,
          titulo: d.titulo,
          inicio,
          fim: instanteCivil(d.data, d.fim),
          profissionalId,
          alunoId: SEM_ALUNO.includes(d.tipo) ? null : d.alunoId || null,
          descricao: d.descricao,
          ...(grupo && (todaEquipe ? { todaEquipe: true } : { participantesIds: participantes })),
        },
        chave,
      });
      aoConcluir(id, inicio, profissionalId);
    } catch (e) {
      setConflitos(normalizarErro(e).conflitos ?? []);
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message }), CAMPO_DA_API));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && (
        <Alert variante="urgente" className="animate-surgir">
          {erroGeral}
          <ListaConflitos conflitos={conflitos} />
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Tipo" erro={errors.tipo?.message} obrigatorio>
          <Select {...register("tipo", { onChange: (e: { target: { value: string } }) => SEM_ALUNO.some((t) => t === e.target.value) && escolherAluno(null) })}>
            {TIPOS.map((t) => (
              <option key={t} value={t}>
                {ROTULO_TIPO[t]}
              </option>
            ))}
          </Select>
        </Field>
        <Field rotulo="Título" erro={errors.titulo?.message} obrigatorio>
          <Input autoComplete="off" {...register("titulo")} />
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field rotulo="Data" erro={errors.data?.message} dica="Segunda a sexta-feira." obrigatorio>
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
      {!coordenacao && !emGrupo && (
        <p className="text-sm text-suave">
          Para: <strong className="font-semibold text-texto">você</strong>
        </p>
      )}
      {coordenacao &&
        !emGrupo &&
        (listaProfissionais ? (
          <Field rotulo="Profissional" erro={errors.profissionalId?.message} obrigatorio>
            <Select {...register("profissionalId")}>
              {listaProfissionais.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.nome}
                </option>
              ))}
            </Select>
          </Field>
        ) : profissionais.isError ? (
          <Alert variante="urgente" className="animate-surgir">Não foi possível carregar os profissionais. Feche e tente novamente.</Alert>
        ) : (
          <p role="status" className="text-sm text-suave">
            Carregando profissionais…
          </p>
        ))}
      {comGrupo && listaProfissionais && (usuarioId || coordenacao) && (
        <fieldset className="flex flex-col gap-2">
          <legend className="mb-1 text-sm font-medium text-texto">Participantes</legend>
          <label className="flex cursor-pointer items-center gap-2 text-sm pointer-coarse:min-h-11">
            <input type="checkbox" className="size-4" checked={todaEquipe} onChange={(e) => setTodaEquipe(e.target.checked)} />
            Toda a equipe
          </label>
          {!todaEquipe && (
            <div className="grid gap-1 sm:grid-cols-2">
              {listaProfissionais.map((p) => (
                <label key={p.id} className="flex cursor-pointer items-center gap-2 text-sm pointer-coarse:min-h-11">
                  <input
                    type="checkbox"
                    className="size-4"
                    checked={participantes.includes(p.id)}
                    disabled={!coordenacao && p.id === usuarioId}
                    aria-describedby={!coordenacao && p.id === usuarioId ? `${idHorarios}-voce` : undefined}
                    onChange={() => alternarParticipante(p.id)}
                  />
                  {p.nome}
                  {!coordenacao && p.id === usuarioId && (
                    <span id={`${idHorarios}-voce`} className="text-xs text-suave">
                      Você precisa participar do grupo que cria
                    </span>
                  )}
                </label>
              ))}
            </div>
          )}
          {coordenacao && !emGrupo && <p className="text-xs text-suave">Sem participantes, o compromisso é só do profissional escolhido.</p>}
        </fieldset>
      )}
      {!semAluno && <BuscaAluno aluno={aluno} aoEscolher={escolherAluno} erro={errors.alunoId?.message} obrigatorio={tipo === "Atendimento"} />}
      <Field rotulo="Descrição" erro={errors.descricao?.message} dica="Notas clínicas pertencem ao atendimento, não à agenda.">
        <Textarea {...register("descricao")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        {aoCancelar && (
          <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
            Cancelar
          </Button>
        )}
        <Button type="submit" carregando={isSubmitting} disabled={coordenacao && !listaProfissionais}>
          Agendar
        </Button>
      </div>
    </form>
  );
}

const esquemaEdicao = z.object({
  titulo: z.string().trim().min(1, "Informe o título.").max(120, "Use no máximo 120 caracteres."),
  descricao: textoOpcional,
});
type EntradaEdicao = z.input<typeof esquemaEdicao>;
type SaidaEdicao = z.output<typeof esquemaEdicao>;
const CAMPOS_EDICAO = Object.keys(esquemaEdicao.shape) as (keyof EntradaEdicao)[];

/** Edição: a API só altera título e descrição; horário muda por remarcação. */
export function EditarCompromissoForm({
  compromisso,
  escopo,
  aoConcluir,
  aoCancelar,
}: {
  compromisso: Compromisso;
  /** Compromisso de grupo: `Grupo` altera todas as instâncias agendadas. */
  escopo?: EscopoGrupo;
  aoConcluir: () => void;
  aoCancelar?: () => void;
}) {
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const atualizar = useAtualizarCompromisso();
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<EntradaEdicao, unknown, SaidaEdicao>({
    resolver: zodResolver(esquemaEdicao),
    defaultValues: { titulo: compromisso.titulo, descricao: compromisso.descricao ?? "" },
  });

  // Aberto ao trocar de modo dentro do mesmo Modal: o foco vai para o primeiro campo.
  useEffect(() => setFocus("titulo"), [setFocus]);

  async function enviar(body: SaidaEdicao) {
    setErroGeral(null);
    try {
      await atualizar.mutateAsync({ id: compromisso.id, body, versao: compromisso.versao, escopo });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS_EDICAO, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
        <Dado rotulo="Tipo">{ROTULO_TIPO[compromisso.tipo]}</Dado>
        <Dado rotulo="Horário">
          {formatarData(compromisso.inicio)} · {formatarIntervalo(compromisso.inicio, compromisso.fim)}
        </Dado>
      </dl>
      <Field rotulo="Título" erro={errors.titulo?.message} obrigatorio>
        <Input autoComplete="off" {...register("titulo")} />
      </Field>
      <Field rotulo="Descrição" erro={errors.descricao?.message} dica="Notas clínicas pertencem ao atendimento, não à agenda.">
        <Textarea {...register("descricao")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        {/* "Voltar", como nos outros modos do detalhe: "Cancelar" ao lado de "Cancelar compromisso" confundia. */}
        {aoCancelar && (
          <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
            Voltar
          </Button>
        )}
        <Button type="submit" carregando={isSubmitting}>
          Salvar
        </Button>
      </div>
    </form>
  );
}
