import { History } from "lucide-react";
import { useState } from "react";
import { classeSecao } from "../../../components/ui/Card";
import { Button } from "../../../components/ui/Button";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Skeleton } from "../../../components/ui/Skeleton";
import { cn } from "../../../lib/cn";
import { formatarData } from "../../../lib/format";
import { useLinhaDoTempo } from "../hooks/useAlunos";

const COR_TIPO: Record<string, string> = { situacao: "bg-info", curso: "bg-atividade", atendimento: "bg-realizado", encaminhamento: "bg-aguardando" };

function PaginaLinhaDoTempo({ alunoId, pagina, ultima, aoCarregarMais }: { alunoId: string; pagina: number; ultima: boolean; aoCarregarMais: () => void }) {
  const consulta = useLinhaDoTempo(alunoId, pagina);

  if (consulta.isPending)
    return (
      <>
        {Array.from({ length: 4 }, (_, i) => (
          <li key={i} className="grid grid-cols-[88px_16px_1fr] gap-3 pb-4">
            <Skeleton className="h-4" />
            <Skeleton className="col-start-3 h-9" />
          </li>
        ))}
      </>
    );
  if (consulta.isError)
    return (
      <li className="pb-4">
        <ErrorState erro={consulta.error} aoTentarNovamente={() => void consulta.refetch()} />
      </li>
    );

  const { itens, tamanho, total } = consulta.data;
  if (pagina === 1 && total === 0)
    return (
      <li>
        <EmptyState icone={<History />} titulo="Nenhum acontecimento registrado" />
      </li>
    );
  return (
    <>
      {/* Os acontecimentos chegam em cascata curta (até 240 ms) para a lista se ler como sequência; em prefers-reduced-motion sobra só o fade. */}
      {itens.map((ev, i) => (
        <li key={`${ev.em}-${ev.tipo}-${i}`} className="animate-surgir grid grid-cols-[88px_16px_1fr] gap-3 pb-4" style={{ animationDelay: `${Math.min(i, 6) * 40}ms` }}>
          <time dateTime={ev.em} className="pt-0.5 text-rotulo text-suave tabular-nums">
            {formatarData(ev.em)}
          </time>
          <div aria-hidden="true" className="flex flex-col items-center">
            <span className={cn("mt-1 size-3 flex-none rounded-full", COR_TIPO[ev.tipo] ?? "bg-suave")} />
            <span className="mt-1 w-0.5 flex-1 bg-borda" />
          </div>
          <div className="text-corpo leading-snug">
            <strong className="font-semibold">{ev.titulo}</strong>
            <div className="text-rotulo text-suave">{[ev.resumo, ev.autor].filter(Boolean).join(" · ")}</div>
          </div>
        </li>
      ))}
      {ultima && pagina * tamanho < total && (
        <li>
          <Button variante="secundaria" tamanho="sm" onClick={aoCarregarMais}>
            Carregar mais
          </Button>
        </li>
      )}
    </>
  );
}

export function LinhaDoTempo({ alunoId }: { alunoId: string }) {
  const [paginas, setPaginas] = useState(1);
  return (
    <section aria-labelledby="titulo-linha-do-tempo" className={cn(classeSecao, "md:min-h-0 md:flex-1")}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="titulo-linha-do-tempo" className="m-0 font-titulo text-base font-bold">
          Linha do tempo
        </h2>
        <span className="text-xs text-suave">acontecimentos, datas e autoria · resumos permitidos ao seu perfil</span>
      </div>
      {/* No desktop a seção ocupa o resto da tela e só a lista rola; "Carregar mais" fica no fim dela. */}
      <ol className="m-0 list-none p-0 md:min-h-0 md:overflow-y-auto">
        {Array.from({ length: paginas }, (_, i) => (
          <PaginaLinhaDoTempo key={i + 1} alunoId={alunoId} pagina={i + 1} ultima={i + 1 === paginas} aoCarregarMais={() => setPaginas((p) => p + 1)} />
        ))}
      </ol>
    </section>
  );
}
