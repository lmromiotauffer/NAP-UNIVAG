import { zodResolver } from "@hookform/resolvers/zod";
import { Plus, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { pode } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Alert } from "../../../components/ui/Alert";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { ConfirmDialog } from "../../../components/ui/ConfirmDialog";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Field } from "../../../components/ui/Field";
import { classeData, classeFiltro, Input } from "../../../components/ui/Input";
import { Modal } from "../../../components/ui/Modal";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Select } from "../../../components/ui/Select";
import { Skeleton } from "../../../components/ui/Skeleton";
import { classeMolduraRolavel, Table, th, td } from "../../../components/ui/Table";
import { Textarea } from "../../../components/ui/Textarea";
import { useToast } from "../../../components/ui/Toast";
import { tratarErro } from "../../../lib/api";
import { cn } from "../../../lib/cn";
import { formatarDataHora, instanteCivil } from "../../../lib/format";
import type { AutorizacaoForaEscala } from "../../../lib/types";
import { useProfissionais } from "../../compartilhado/catalogos";
import { useCriarAutorizacao, useListaAutorizacoes, useRevogarAutorizacao } from "../hooks/useAdministracao";

const DOZE_HORAS = 12 * 60 * 60 * 1000;

const esquema = z.object({
  profissionalId: z.string().min(1, "Selecione o profissional."),
  inicio: z.string().min(1, "Informe o início."),
  fim: z.string().min(1, "Informe o fim."),
  justificativa: z.string().trim().min(5, "Informe a justificativa (mínimo 5 caracteres).").max(500, "A justificativa deve ter no máximo 500 caracteres."),
});
type Dados = z.infer<typeof esquema>;
const CAMPOS = ["profissionalId", "inicio", "fim", "justificativa"] as const;

/** "2026-10-11T08:00" (datetime-local, relógio de Cuiabá) → ISO com o deslocamento do fuso. */
const instante = (local: string) => instanteCivil(local.slice(0, 10), local.slice(11, 16));

function NovaAutorizacaoForm({ aoCancelar, aoConcluir }: { aoCancelar: () => void; aoConcluir: () => void }) {
  // Uma chave por formulário: retentativas reaproveitam a mesma.
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const profissionais = useProfissionais();
  const criar = useCriarAutorizacao();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Dados>({ resolver: zodResolver(esquema), defaultValues: { profissionalId: "", inicio: "", fim: "", justificativa: "" } });

  async function enviar(d: Dados) {
    setErroGeral(null);
    const inicio = instante(d.inicio);
    const fim = instante(d.fim);
    const duracao = new Date(fim).getTime() - new Date(inicio).getTime();
    if (duracao <= 0) return setError("fim", { message: "O fim deve ser depois do início." });
    if (duracao > DOZE_HORAS) return setError("fim", { message: "A autorização dura no máximo 12 horas." });
    try {
      await criar.mutateAsync({ body: { profissionalId: d.profissionalId, inicio, fim, justificativa: d.justificativa }, chave });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente">{erroGeral}</Alert>}
      <Field rotulo="Profissional" erro={errors.profissionalId?.message} obrigatorio>
        <Select {...register("profissionalId")}>
          <option value="">Selecione</option>
          {profissionais.data?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nome}
            </option>
          ))}
        </Select>
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Início" erro={errors.inicio?.message} obrigatorio>
          <Input type="datetime-local" {...register("inicio")} />
        </Field>
        <Field rotulo="Fim" erro={errors.fim?.message} dica="Até 12 horas." obrigatorio>
          <Input type="datetime-local" {...register("fim")} />
        </Field>
      </div>
      <Field rotulo="Justificativa" erro={errors.justificativa?.message} obrigatorio>
        <Textarea maxLength={500} {...register("justificativa")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Cancelar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Autorizar
        </Button>
      </div>
    </form>
  );
}

/** Autorizações de trabalho fora da escala: só quem gere a produção (Administrador e CoordenadorNap). */
export function AutorizacoesForaEscala() {
  const { usuario } = useSessao();
  const permitido = pode(usuario, "producao.gerir");
  const toast = useToast();
  const [profissionalId, setProfissionalId] = useState("");
  const [inicio, setInicio] = useState("");
  const [fim, setFim] = useState("");
  const [pagina, setPagina] = useState(1);
  const [nova, setNova] = useState(false);
  const [revogando, setRevogando] = useState<AutorizacaoForaEscala | null>(null);
  const profissionais = useProfissionais();
  const lista = useListaAutorizacoes({ profissionalId: profissionalId || undefined, inicio: inicio || undefined, fim: fim || undefined, pagina, tamanho: 10 }, permitido);
  const revogar = useRevogarAutorizacao();

  if (!permitido) return null;

  async function confirmarRevogacao() {
    if (!revogando) return;
    try {
      await revogar.mutateAsync(revogando.id);
      toast.sucesso("Autorização revogada.");
    } catch (e) {
      toast.erro(tratarErro(e, [] as const, () => {}) ?? "Não foi possível revogar a autorização.");
    }
    setRevogando(null);
  }

  const filtrar = (definir: (v: string) => void) => (e: { target: { value: string } }) => {
    definir(e.target.value);
    setPagina(1);
  };

  let corpo;
  if (lista.isError) {
    corpo = (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  } else if (lista.data?.itens.length === 0 && !lista.isPlaceholderData) {
    corpo = <EmptyState icone={<ShieldCheck />} titulo="Nenhuma autorização" descricao="Sem autorização, o atendimento só pode começar dentro da escala do profissional." />;
  } else {
    corpo = (
      <Table caption="Autorizações fora da escala" aria-busy={lista.isPending || lista.isPlaceholderData} className={cn(lista.isPlaceholderData && "opacity-60")}>
        <thead>
          <tr className="border-b border-borda">
            <th scope="col" className={th}>Profissional</th>
            <th scope="col" className={th}>Período</th>
            <th scope="col" className={th}>Justificativa</th>
            <th scope="col" className={th}>Situação</th>
            <th scope="col" className={th}>
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {!lista.data &&
            Array.from({ length: 3 }, (_, i) => (
              <tr key={i} aria-hidden="true" className="border-b border-borda">
                <td className={td}>
                  <Skeleton className="h-4 w-40" />
                  <Skeleton className="mt-1.5 h-3 w-32" />
                </td>
                <td className={td}>
                  <Skeleton className="h-4 w-48" />
                </td>
                <td className={td}>
                  <Skeleton className="h-4 w-56" />
                </td>
                <td className={td}>
                  <Skeleton className="h-5 w-16 rounded-full" />
                </td>
                <td className={td} />
              </tr>
            ))}
          {lista.data?.itens.map((a) => (
            <tr key={a.id} className="border-b border-borda text-corpo">
              <td className={td}>
                <span className="font-semibold">{a.profissional}</span>
                <div className="text-xs text-suave">Autorizada por {a.autorizadoPor}</div>
              </td>
              <td className={td}>
                {formatarDataHora(a.inicio)} a {formatarDataHora(a.fim)}
              </td>
              <td className={td}>{a.justificativa}</td>
              <td className={td}>{a.revogadaEm ? <Badge>Revogada</Badge> : <Badge variante="realizado">Ativa</Badge>}</td>
              <td className={`${td} text-right`}>
                {!a.revogadaEm && (
                  <Button tamanho="sm" variante="secundaria" aria-label={`Revogar autorização de ${a.profissional}`} onClick={() => setRevogando(a)}>
                    Revogar
                  </Button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </Table>
    );
  }

  return (
    <section aria-labelledby="autorizacoes-titulo" className="mt-8 flex flex-col gap-3 md:min-h-0 md:flex-1">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 id="autorizacoes-titulo" className="m-0 text-base font-semibold">
            Autorizações fora da escala
          </h2>
          <p className="m-0 text-sm text-suave">Permitem registrar atendimento fora da escala ou em feriado. Registros já feitos não mudam ao revogar.</p>
        </div>
        <Button onClick={() => setNova(true)} className="rounded-full">
          <Plus aria-hidden="true" className="size-4" />
          Nova autorização
        </Button>
      </div>
      {/* Mesma barra das outras listagens (Usuários, Auditoria): pílulas, rótulo do profissional só para leitores de tela. */}
      <div className="flex flex-wrap items-center gap-2">
        <label htmlFor="autorizacoes-profissional" className="sr-only">
          Profissional
        </label>
        <Select id="autorizacoes-profissional" className={classeFiltro} value={profissionalId} onChange={filtrar(setProfissionalId)}>
          <option value="">Profissional: todos</option>
          {profissionais.data?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nome}
            </option>
          ))}
        </Select>
        <div className="flex w-full min-w-0 flex-wrap items-center gap-1.5 text-denso text-suave sm:w-auto">
          <span className="flex flex-1 items-center gap-1.5">
            <label htmlFor="autorizacoes-inicio">De</label>
            <Input id="autorizacoes-inicio" type="date" className={classeData} value={inicio} onChange={filtrar(setInicio)} />
          </span>
          <span className="flex flex-1 items-center gap-1.5">
            <label htmlFor="autorizacoes-fim">Até</label>
            <Input id="autorizacoes-fim" type="date" className={classeData} value={fim} onChange={filtrar(setFim)} />
          </span>
        </div>
      </div>
      <div className={classeMolduraRolavel}>
        {corpo}
        {lista.data && lista.data.total > lista.data.tamanho && (
          <div className="px-4 md:mt-auto">
            <Paginacao pagina={lista.data.pagina} tamanho={lista.data.tamanho} total={lista.data.total} aoMudar={setPagina} />
          </div>
        )}
      </div>
      <Modal aberto={nova} aoFechar={() => setNova(false)} titulo="Nova autorização">
        {nova && (
          <NovaAutorizacaoForm
            aoCancelar={() => setNova(false)}
            aoConcluir={() => {
              toast.sucesso("Autorização criada.");
              setNova(false);
            }}
          />
        )}
      </Modal>
      <ConfirmDialog
        aberto={revogando !== null}
        titulo="Revogar autorização"
        mensagem={`Revogar a autorização de ${revogando?.profissional ?? ""}? Registros já feitos não são apagados.`}
        confirmar="Revogar"
        aoConfirmar={() => void confirmarRevogacao()}
        aoCancelar={() => setRevogando(null)}
        carregando={revogar.isPending}
      />
    </section>
  );
}
