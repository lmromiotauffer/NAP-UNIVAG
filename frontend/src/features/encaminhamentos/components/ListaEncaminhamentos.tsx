import { ChevronRight, Send } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Skeleton } from "../../../components/ui/Skeleton";
import { Table, th, td, soMedio, trClicavel } from "../../../components/ui/Table";
import { cn } from "../../../lib/cn";
import { formatarDataCivil, formatarInteiro, hojeCivil } from "../../../lib/format";
import type { EncaminhamentoLista } from "../../../lib/types";
import { useListaEncaminhamentos } from "../hooks/useEncaminhamentos";
import { diasEntre, especialidadeEDestino, NAO_COMPARTILHADO, ROTULO_DESTINO, ROTULO_ESPECIALIDADE, ROTULO_ESTADO, VARIANTE_ESTADO } from "../rotulos";
import type { FiltroEncaminhamentos } from "../services/encaminhamentoService";

/** Estado sempre em texto; o selo de prazo fica ao lado, sem substituir o estado. */
export function SeloEstado({ estado, data, semAcolhimentoNoPrazo }: Pick<EncaminhamentoLista, "estado" | "data" | "semAcolhimentoNoPrazo">) {
  const dias = diasEntre(data, hojeCivil());
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <Badge variante={VARIANTE_ESTADO[estado]} className="whitespace-nowrap">
        {ROTULO_ESTADO[estado]}
      </Badge>
      {semAcolhimentoNoPrazo && (
        <Badge variante="urgente" className="whitespace-nowrap">
          Sem acolhimento há {dias} {dias === 1 ? "dia" : "dias"}
        </Badge>
      )}
    </span>
  );
}

/** Tabela paginada de encaminhamentos (a listagem nunca traz motivo restrito nem observações). */
export function ListaEncaminhamentos({
  filtro,
  comFiltro,
  semAluno,
  vazio,
  aoMudarPagina,
}: {
  filtro: FiltroEncaminhamentos;
  comFiltro: boolean;
  /** Na ficha do aluno a coluna Aluno é redundante. */
  semAluno?: boolean;
  vazio: { titulo: string; descricao: string; acao?: ReactNode; acaoFiltro?: ReactNode };
  aoMudarPagina: (pagina: number) => void;
}) {
  const lista = useListaEncaminhamentos(filtro);
  const colunas = [...(semAluno ? [] : [""]), soMedio, soMedio, "", ""];

  if (lista.isError && !lista.data)
    return (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  if (lista.data && lista.data.total === 0 && !lista.isPlaceholderData)
    return comFiltro ? (
      <EmptyState icone={<Send />} titulo="Nenhum encaminhamento corresponde aos filtros" descricao="Revise os filtros selecionados." acao={vazio.acaoFiltro} />
    ) : (
      <EmptyState icone={<Send />} titulo={vazio.titulo} descricao={vazio.descricao} acao={vazio.acao} />
    );
  if (lista.data && lista.data.itens.length === 0 && !lista.isPlaceholderData)
    return (
      <EmptyState
        icone={<Send />}
        titulo="Esta página não tem encaminhamentos"
        descricao={`A lista tem ${formatarInteiro(lista.data.total)} ${lista.data.total === 1 ? "encaminhamento" : "encaminhamentos"}.`}
        acao={
          <Button variante="secundaria" tamanho="sm" onClick={() => aoMudarPagina(1)}>
            Voltar à primeira página
          </Button>
        }
      />
    );

  return (
    <>
      <Table caption="Encaminhamentos" aria-busy={lista.isPending || lista.isPlaceholderData} className={cn(lista.isPlaceholderData && "opacity-60")}>
        <thead>
          <tr className="border-b border-borda">
            {!semAluno && <th scope="col" className={th}>Aluno</th>}
            <th scope="col" className={cn(th, soMedio)}>Especialidade → destino</th>
            <th scope="col" className={cn(th, soMedio)}>Envio</th>
            <th scope="col" className={th}>Estado</th>
            <th scope="col" className={th}>
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {lista.data
            ? lista.data.itens.map((e) => (
                <tr key={e.id} className={trClicavel}>
                  {!semAluno && (
                    <td className={td}>
                      {/* A partir de `sm` o curso fica na linha do nome; no celular desce com especialidade e envio. */}
                      <div className="flex flex-wrap items-baseline gap-x-1.5">
                        <span className="font-semibold">{e.aluno}</span>
                        <span className="text-rotulo text-suave max-sm:hidden">· {e.curso || "—"}</span>
                      </div>
                      <div className="text-rotulo text-suave sm:hidden">
                        {e.curso || "—"} · {especialidadeEDestino(e)} · {formatarDataCivil(e.data)}
                      </div>
                    </td>
                  )}
                  <td className={cn(td, soMedio, "text-rotulo")}>
                    {e.especialidade && e.destino ? (
                      <>
                        {ROTULO_ESPECIALIDADE[e.especialidade]} <span className="text-suave">→ {ROTULO_DESTINO[e.destino]}</span>
                      </>
                    ) : (
                      <span className="text-suave">{NAO_COMPARTILHADO}</span>
                    )}
                  </td>
                  <td className={cn(td, soMedio, "text-rotulo whitespace-nowrap text-suave tabular-nums")}>{formatarDataCivil(e.data)}</td>
                  <td className={td}>
                    <SeloEstado {...e} />
                  </td>
                  <td className={cn(td, "text-right max-sm:pl-0")}>
                    <Link
                      to={`/encaminhamentos/${e.id}`}
                      aria-label={`Abrir encaminhamento de ${e.aluno} (${e.especialidade ? ROTULO_ESPECIALIDADE[e.especialidade] : NAO_COMPARTILHADO}, ${formatarDataCivil(e.data)})`}
                      className="inline-flex items-center gap-1 rounded-sm text-xs font-semibold whitespace-nowrap text-primaria after:absolute after:inset-0 after:content-['']"
                    >
                      {/* O ::after estende o link à linha toda: o alvo de toque deixa de ser só a seta. */}
                      <span className="hidden lg:inline">Abrir</span>
                      <ChevronRight aria-hidden="true" className="size-3.5" />
                    </Link>
                  </td>
                </tr>
              ))
            : Array.from({ length: 6 }, (_, i) => (
                <tr key={i} aria-hidden="true" className="border-b border-borda">
                  {colunas.map((classe, j) => (
                    <td key={j} className={cn(td, classe)}>
                      <Skeleton className="h-4 w-full max-w-28" />
                    </td>
                  ))}
                </tr>
              ))}
        </tbody>
      </Table>
      {lista.data && (
        <div className="px-4 md:mt-auto">
          <Paginacao pagina={filtro.pagina ?? 1} tamanho={lista.data.tamanho} total={lista.data.total} aoMudar={aoMudarPagina} />
        </div>
      )}
      {lista.isError && (
        <div className="p-4">
          <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
        </div>
      )}
    </>
  );
}
