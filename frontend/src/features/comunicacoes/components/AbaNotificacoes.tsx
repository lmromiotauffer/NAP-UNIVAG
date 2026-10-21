import { Bell } from "lucide-react";
import { useNavigate, useSearchParams } from "react-router";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Carregando } from "../../../components/ui/Carregando";
import { Skeleton } from "../../../components/ui/Skeleton";
import { classeMolduraRolavel } from "../../../components/ui/Table";
import { useToast } from "../../../components/ui/Toast";
import { cn } from "../../../lib/cn";
import { formatarDataHora } from "../../../lib/format";
import { comParametro, paginaDaUrl } from "../../../lib/queryKeys";
import type { Notificacao } from "../../../lib/types";
import { useListaNotificacoes, useMarcarLida, useMarcarTodasLidas } from "../../notificacoes/hooks/useNotificacoes";

const TAMANHO = 20;

/** Só caminhos internos: o link vem da API, mas nunca leva para fora do sistema. */
const ehCaminhoInterno = (l: string) => l.startsWith("/") && !l.startsWith("//");

/** Aba Notificações de Comunicações (era a página /notificacoes). Filtro e página ficam na URL, ao lado de `aba`. */
export function AbaNotificacoes() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const toast = useToast();
  const soNaoLidas = params.get("naoLidas") === "1";
  const pagina = paginaDaUrl(params) ?? 1;
  const lista = useListaNotificacoes({ naoLidas: soNaoLidas || undefined, pagina, tamanho: TAMANHO });
  const marcarLida = useMarcarLida();
  const marcarTodas = useMarcarTodasLidas();

  const alterar = (chave: string, valor: string | null) => setParams((atual) => comParametro(atual, chave, valor));

  async function abrir(n: Notificacao) {
    // Marcar é melhor esforço: falhar não deve impedir de chegar ao encaminhamento.
    if (!n.lida) await marcarLida.mutateAsync(n.id).catch(() => undefined);
    if (ehCaminhoInterno(n.link)) void navigate(n.link);
  }

  async function lerTodas() {
    try {
      await marcarTodas.mutateAsync();
      toast.sucesso("Todas as notificações foram marcadas como lidas.");
    } catch {
      toast.erro("Não foi possível marcar as notificações como lidas.");
    }
  }

  let corpo;
  if (lista.isError && !lista.data) {
    corpo = (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  } else if (lista.isPending) {
    corpo = (
      <Carregando rotulo="Carregando notificações" className="flex flex-col gap-2 p-4">
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </Carregando>
    );
  } else if (lista.data.itens.length === 0 && !lista.isPlaceholderData) {
    corpo =
      lista.data.total > 0 ? (
        <EmptyState
          icone={<Bell />}
          titulo="Esta página não tem notificações"
          acao={
            <Button variante="secundaria" tamanho="sm" onClick={() => alterar("pagina", null)}>
              Voltar à primeira página
            </Button>
          }
        />
      ) : soNaoLidas ? (
        <EmptyState icone={<Bell />} titulo="Nenhuma notificação não lida" descricao="Você está em dia." />
      ) : (
        <EmptyState icone={<Bell />} titulo="Nenhuma notificação" descricao="Os avisos do sistema, como novos encaminhamentos e mensagens, aparecem aqui." />
      );
  } else {
    corpo = (
      // A lista monta depois do skeleton: chega com animate-surgir; a troca de página só esmaece (keepPreviousData).
      <ul aria-busy={lista.isPlaceholderData} className={cn("animate-surgir m-0 flex list-none flex-col p-0 transition-opacity duration-150 md:min-h-0 md:overflow-y-auto", lista.isPlaceholderData && "opacity-60")}>
        {lista.data.itens.map((n) => (
          <li key={n.id} className="flex flex-wrap items-start justify-between gap-3 border-b border-borda px-4 py-2 last:border-b-0">
            <div className="min-w-0 flex-1 text-corpo">
              <div className="flex flex-wrap items-center gap-2">
                {!n.lida && <Badge variante="info">Não lida</Badge>}
                <span className={cn("break-words", n.lida ? "font-medium" : "font-semibold")}>{n.titulo}</span>
              </div>
              <p className="m-0 mt-0.5 break-words text-suave">{n.texto}</p>
              <p className="m-0 mt-0.5 text-xs text-suave">{formatarDataHora(n.criadaEm)}</p>
            </div>
            <Button variante="secundaria" tamanho="sm" aria-label={`Abrir: ${n.titulo}`} onClick={() => void abrir(n)}>
              Abrir
            </Button>
          </li>
        ))}
      </ul>
    );
  }

  return (
    <div className="flex flex-col gap-[18px] md:min-h-0 md:flex-1">
      <div className="flex flex-wrap items-center justify-end gap-3">
        <label className="flex cursor-pointer items-center gap-2 text-sm pointer-coarse:min-h-11">
          <input type="checkbox" checked={soNaoLidas} onChange={(e) => alterar("naoLidas", e.target.checked ? "1" : null)} className="size-4 accent-primaria" />
          Só não lidas
        </label>
        <Button variante="secundaria" onClick={() => void lerTodas()} carregando={marcarTodas.isPending} disabled={lista.data?.total === 0}>
          Marcar todas como lidas
        </Button>
      </div>
      <section aria-label="Lista de notificações" className={classeMolduraRolavel}>
        {corpo}
        {lista.data && lista.data.total > TAMANHO && (
          <div className="px-4 md:mt-auto">
            <Paginacao pagina={lista.data.pagina} tamanho={lista.data.tamanho} total={lista.data.total} aoMudar={(p) => alterar("pagina", p > 1 ? String(p) : null)} />
          </div>
        )}
      </section>
      {lista.isError && lista.data && <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />}
      <p className="m-0 text-rotulo text-suave">Ler uma notificação não equivale a dar ciência.</p>
    </div>
  );
}
