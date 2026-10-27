import { ChevronRight } from "lucide-react";
import type { ReactNode } from "react";
import { Link, useLocation } from "react-router";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Carregando } from "../../../components/ui/Carregando";
import { Skeleton } from "../../../components/ui/Skeleton";
import { cn } from "../../../lib/cn";
import { formatarDataHora, formatarInteiro } from "../../../lib/format";
import type { MensagemLista } from "../../../lib/types";
import { useCaixa } from "../hooks/useComunicacoes";
import { ROTULO_TIPO } from "../rotulos";
import type { FiltroCaixa } from "../services/comunicacaoService";

const naoLida = (m: MensagemLista) => m.recebida && !m.lida;

/** Lista paginada de uma caixa; cada linha abre o detalhe (rascunho abre para edição). */
export function CaixaMensagens({
  filtro,
  comBusca,
  vazio,
  aoMudarPagina,
}: {
  filtro: FiltroCaixa;
  comBusca: boolean;
  vazio: { icone: ReactNode; titulo: string; descricao: string };
  aoMudarPagina: (pagina: number) => void;
}) {
  const lista = useCaixa(filtro);
  // O detalhe volta para onde a lista estava (aba e página da caixa, ou a ficha do aluno).
  const { pathname, search } = useLocation();

  if (lista.isError && !lista.data)
    return (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  if (lista.isPending)
    return (
      <Carregando rotulo="Carregando mensagens" className="flex flex-col gap-2 p-4">
        <Skeleton className="h-14" />
        <Skeleton className="h-14" />
        <Skeleton className="h-14" />
      </Carregando>
    );
  if (lista.data.total === 0 && !lista.isPlaceholderData)
    return comBusca ? (
      <EmptyState icone={vazio.icone} titulo="Nenhuma mensagem corresponde à busca" descricao="Revise o termo buscado." />
    ) : (
      <EmptyState icone={vazio.icone} titulo={vazio.titulo} descricao={vazio.descricao} />
    );
  if (lista.data.itens.length === 0 && !lista.isPlaceholderData)
    return (
      <EmptyState
        icone={vazio.icone}
        titulo="Esta página não tem mensagens"
        descricao={`A caixa tem ${formatarInteiro(lista.data.total)} ${lista.data.total === 1 ? "mensagem" : "mensagens"}.`}
        acao={
          <Button variante="secundaria" tamanho="sm" onClick={() => aoMudarPagina(1)}>
            Voltar à primeira página
          </Button>
        }
      />
    );

  return (
    <>
      {/* A lista substitui o skeleton sem animação de entrada (apagaria e surgiria de novo); a troca de página só esmaece (keepPreviousData). */}
      <ul aria-busy={lista.isPlaceholderData} className={cn("m-0 flex list-none flex-col p-0 transition-opacity duration-150 md:min-h-0 md:overflow-y-auto", lista.isPlaceholderData && "opacity-60")}>
        {lista.data.itens.map((m) => (
          <li key={m.id} className="relative flex items-center gap-3 border-b border-borda px-4 py-2 text-corpo transition-colors duration-150 last:border-b-0 hover:bg-realce">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                {naoLida(m) && <Badge variante="info">Não lida</Badge>}
                {/* O ::after estende o link à linha toda. */}
                <Link
                  to={`/comunicacoes/${m.id}`}
                  state={{ voltar: pathname + search }}
                  className={cn(
                    "min-w-0 break-words rounded-sm after:absolute after:inset-0 after:content-['']",
                    naoLida(m) ? "font-semibold" : "font-medium",
                  )}
                >
                  {m.assunto}
                </Link>
              </div>
              <p className="m-0 mt-0.5 break-words text-suave">
                {m.recebida ? `De ${m.remetente}` : m.destinatario ? `Para ${m.destinatario}` : "Sem destinatário"} · {ROTULO_TIPO[m.tipo]}
                {m.aluno && ` · ${m.aluno}`}
              </p>
            </div>
            <span className="flex-none text-xs whitespace-nowrap text-suave tabular-nums">{formatarDataHora(m.data)}</span>
            <ChevronRight aria-hidden="true" className="size-3.5 flex-none text-primaria" />
          </li>
        ))}
      </ul>
      {lista.data.total > lista.data.tamanho && (
        <div className="px-4 md:mt-auto">
          <Paginacao pagina={lista.data.pagina} tamanho={lista.data.tamanho} total={lista.data.total} aoMudar={aoMudarPagina} />
        </div>
      )}
      {lista.isError && (
        <div className="p-4">
          <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} className="animate-surgir" />
        </div>
      )}
    </>
  );
}
