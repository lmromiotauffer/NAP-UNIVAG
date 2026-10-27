import { ChevronLeft } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { classeSecao, classeTituloPagina } from "../../components/ui/Card";
import { ConfirmDialog } from "../../components/ui/ConfirmDialog";
import { ErrorState } from "../../components/ui/ErrorState";
import { Carregando } from "../../components/ui/Carregando";
import { Skeleton } from "../../components/ui/Skeleton";
import { useToast } from "../../components/ui/Toast";
import { mensagemErro } from "../../lib/api";
import { cn } from "../../lib/cn";
import { formatarDataHora } from "../../lib/format";
import { destinoSeguro } from "../../lib/navegacao";
import type { MensagemDetalhe } from "../../lib/types";
import { EscreverMensagem } from "./components/EscreverMensagem";
import { ResponderMensagem } from "./components/ResponderMensagem";
import { useArquivarMensagem, useDescartarRascunho, useDetalheMensagem, useMarcarMensagemLida } from "./hooks/useComunicacoes";
import { ROTULO_TIPO } from "./rotulos";

function Rascunho({ m }: { m: MensagemDetalhe }) {
  const navigate = useNavigate();
  const toast = useToast();
  const descartar = useDescartarRascunho();
  const [confirmando, setConfirmando] = useState(false);

  async function confirmarDescarte() {
    try {
      await descartar.mutateAsync(m.id);
      toast.sucesso("Rascunho descartado.");
      void navigate("/comunicacoes?aba=rascunhos");
    } catch (e) {
      toast.erro(mensagemErro(e));
      setConfirmando(false);
    }
  }

  return (
    <>
      <header className="flex flex-wrap items-start justify-between gap-4">
        <h1 className={classeTituloPagina}>Rascunho</h1>
        <Button variante="secundaria" className="rounded-full text-urgente" onClick={() => setConfirmando(true)}>
          Descartar rascunho
        </Button>
      </header>
      <section aria-label="Editar rascunho" className={cn(classeSecao, "max-w-3xl")}>
        <EscreverMensagem
          rascunho={m}
          aoConcluir={(acao, id) => {
            if (acao === "enviada") {
              toast.sucesso("Mensagem enviada.");
              void navigate(`/comunicacoes/${id}`, { replace: true });
            } else {
              toast.sucesso("Rascunho salvo.");
              void navigate("/comunicacoes?aba=rascunhos");
            }
          }}
        />
      </section>
      <ConfirmDialog
        aberto={confirmando}
        titulo="Descartar rascunho"
        mensagem="O rascunho será excluído e não poderá ser recuperado."
        confirmar="Descartar"
        carregando={descartar.isPending}
        aoConfirmar={() => void confirmarDescarte()}
        aoCancelar={() => setConfirmando(false)}
      />
    </>
  );
}

function Enviada({ m }: { m: MensagemDetalhe }) {
  const { usuario } = useSessao();
  const toast = useToast();
  const arquivar = useArquivarMensagem();
  const [respondendo, setRespondendo] = useState(false);
  // Só a resposta que entra depois da montagem chega animada: na abertura a seção já vem com o surgir do <main>.
  const [tamanhoInicialDoFio] = useState(m.fio.length);

  async function alternarArquivo() {
    try {
      await arquivar.mutateAsync({ id: m.id, arquivar: !m.arquivada });
      toast.sucesso(m.arquivada ? "Mensagem desarquivada." : "Mensagem arquivada.");
    } catch (e) {
      toast.erro(mensagemErro(e));
    }
  }

  return (
    <>
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className={cn(classeTituloPagina, "break-words")}>{m.assunto}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-x-1.5 gap-y-1 text-denso text-suave">
            <Badge variante="neutro">{ROTULO_TIPO[m.tipo]}</Badge>
            <span>
              De {m.remetente} para {m.destinatario ?? "—"}
            </span>
            {m.aluno && (
              <span>
                · Aluno:{" "}
                {pode(usuario, "alunos.consultar") && m.alunoId ? (
                  <Link to={`/alunos/${m.alunoId}`} className="rounded-sm text-primaria hover:underline">
                    {m.aluno}
                  </Link>
                ) : (
                  m.aluno
                )}
              </span>
            )}
            {m.encaminhamentoId && pode(usuario, "encaminhamentos") && (
              <span>
                ·{" "}
                <Link to={`/encaminhamentos/${m.encaminhamentoId}`} className="rounded-sm text-primaria hover:underline">
                  Ver encaminhamento
                </Link>
              </span>
            )}
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {m.podeResponder && !respondendo && (
            <Button className="rounded-full" onClick={() => setRespondendo(true)}>
              Responder
            </Button>
          )}
          <Button variante="secundaria" className="rounded-full" carregando={arquivar.isPending} onClick={() => void alternarArquivo()}>
            {m.arquivada ? "Desarquivar" : "Arquivar"}
          </Button>
        </div>
      </header>

      <section aria-label="Conversa" className={cn(classeSecao, "max-w-3xl")}>
        <ol className="m-0 flex list-none flex-col gap-4 p-0">
          {m.fio.map((f, i) => (
            // Fio em ordem cronológica: depois de responder só a resposta nova monta, e chega animada no fim da conversa.
            <li key={f.id} aria-current={f.id === m.id || undefined} className={cn("flex flex-col gap-1 border-b border-borda pb-4 last:border-b-0 last:pb-0", i >= tamanhoInicialDoFio && "animate-surgir")}>
              <div className="flex flex-wrap items-baseline justify-between gap-x-3 text-denso">
                <strong className="font-semibold">{f.remetente}</strong>
                <span className="text-xs text-suave tabular-nums">{formatarDataHora(f.enviadaEm)}</span>
              </div>
              {f.assunto !== m.assunto && <div className="text-xs text-suave">{f.assunto}</div>}
              {/* Texto puro: quebras de linha preservadas, nunca HTML. */}
              <p className="m-0 text-corpo break-words whitespace-pre-wrap">{f.corpo}</p>
            </li>
          ))}
        </ol>
      </section>

      {respondendo && (
        <section aria-label="Responder" className={cn(classeSecao, "max-w-3xl")}>
          <ResponderMensagem
            mensagemId={m.id}
            aoCancelar={() => setRespondendo(false)}
            aoConcluir={() => {
              toast.sucesso("Resposta enviada.");
              setRespondendo(false);
            }}
          />
        </section>
      )}
    </>
  );
}

export function DetalheMensagemPage() {
  const { id = "" } = useParams();
  const detalhe = useDetalheMensagem(id);
  const { mutate: marcarLida } = useMarcarMensagemLida();
  // Uma vez por mensagem: o refetch após marcar (ou o StrictMode) não dispara de novo; falhar não bloqueia a leitura.
  const marcadas = useRef(new Set<string>());
  const m = detalhe.data;
  const origem = (useLocation().state as { voltar?: string } | null)?.voltar;
  const voltarPara = destinoSeguro(origem, "/comunicacoes");

  useEffect(() => {
    if (!m?.podeMarcarLida || marcadas.current.has(m.id)) return;
    marcadas.current.add(m.id);
    marcarLida(m.id);
  }, [m, marcarLida]);

  const voltar = (
    <Link
      to={voltarPara}
      aria-label={voltarPara.startsWith("/comunicacoes") ? "Voltar para Comunicações" : "Voltar"}
      className="inline-flex items-center gap-0.5 self-start rounded-sm text-denso text-suave transition-colors duration-150 hover:text-texto pointer-coarse:min-h-11"
    >
      <ChevronLeft aria-hidden="true" className="-ml-1 size-4" />
      {voltarPara.startsWith("/comunicacoes") ? "Comunicações" : "Voltar"}
    </Link>
  );

  const titulo = <title>{m ? `${m.estado === "Rascunho" ? "Rascunho" : m.assunto} · Comunicações · NAP` : "Comunicações · NAP"}</title>;

  if (detalhe.isPending)
    return (
      <>
        {titulo}
        {voltar}
        <Carregando rotulo="Carregando mensagem" className="flex flex-col gap-[18px]">
          <Skeleton className="h-8 w-72 max-w-full" />
          <Skeleton className="h-4 w-96 max-w-full" />
          <Skeleton className="h-48 max-w-3xl rounded-secao" />
        </Carregando>
      </>
    );
  if (detalhe.isError)
    return (
      <>
        {titulo}
        {voltar}
        <ErrorState erro={detalhe.error} aoTentarNovamente={() => void detalhe.refetch()} />
      </>
    );

  return (
    <>
      {titulo}
      {voltar}
      {detalhe.data.estado === "Rascunho" ? <Rascunho m={detalhe.data} /> : <Enviada m={detalhe.data} />}
    </>
  );
}
