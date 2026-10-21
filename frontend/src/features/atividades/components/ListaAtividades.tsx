import { ChevronRight, Presentation } from "lucide-react";
import type { ReactNode } from "react";
import { Button } from "../../../components/ui/Button";
import { Badge } from "../../../components/ui/Badge";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Skeleton } from "../../../components/ui/Skeleton";
import { Table, th, td, soMedio, trClicavel } from "../../../components/ui/Table";
import { cn } from "../../../lib/cn";
import { formatarData, formatarDuracao, formatarHora } from "../../../lib/format";
import { useListaAtividades } from "../hooks/useAtividades";
import type { FiltroAtividades } from "../services/atividadeService";
import { ROTULO_TIPO_ATIVIDADE, ROTULO_TURNO } from "../rotulos";

// No celular a linha fica compacta: tipo, data, turno e profissional ficam numa linha de detalhes sob o curso.

/** Tabela paginada de atividades; a API já recorta por perfil (coordenação de curso vê só as do curso). */
export function ListaAtividades({
  filtro,
  comFiltro,
  vazio,
  aoMudarPagina,
  aoAbrir,
}: {
  filtro: FiltroAtividades;
  comFiltro: boolean;
  vazio: { titulo: string; descricao: string; acao?: ReactNode; acaoFiltro?: ReactNode };
  aoMudarPagina: (pagina: number) => void;
  aoAbrir: (id: string) => void;
}) {
  const lista = useListaAtividades(filtro);
  const colunas = [soMedio, soMedio, "", "hidden lg:table-cell", "hidden lg:table-cell", ""];

  if (lista.isError && !lista.data)
    return (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  if (lista.data && lista.data.total === 0 && !lista.isPlaceholderData)
    return comFiltro ? (
      <EmptyState icone={<Presentation />} titulo="Nenhuma atividade corresponde aos filtros" descricao="Revise os filtros selecionados." acao={vazio.acaoFiltro} />
    ) : (
      <EmptyState icone={<Presentation />} titulo={vazio.titulo} descricao={vazio.descricao} acao={vazio.acao} />
    );
  if (lista.data && lista.data.itens.length === 0 && !lista.isPlaceholderData)
    // Link antigo com `pagina` além da última: sem a Paginacao montada, este botão é o único caminho de volta.
    return (
      <EmptyState
        icone={<Presentation />}
        titulo="Esta página não tem atividades"
        descricao={`A lista tem ${lista.data.total} ${lista.data.total === 1 ? "atividade" : "atividades"}.`}
        acao={
          <Button variante="secundaria" tamanho="sm" onClick={() => aoMudarPagina(1)}>
            Voltar à primeira página
          </Button>
        }
      />
    );

  return (
    <>
      <Table caption="Atividades nos cursos" aria-busy={lista.isPending || lista.isPlaceholderData} className={cn(lista.isPlaceholderData && "opacity-60")}>
        <thead>
          <tr className="border-b border-borda">
            <th scope="col" className={cn(th, soMedio)}>Data</th>
            <th scope="col" className={cn(th, soMedio)}>Turno · horário</th>
            <th scope="col" className={th}>
              <span className="sm:hidden">Atividade</span>
              <span className="max-sm:hidden">Curso · tipo</span>
            </th>
            <th scope="col" className={cn(th, "hidden lg:table-cell")}>Duração</th>
            <th scope="col" className={cn(th, "hidden lg:table-cell")}>Profissional</th>
            <th scope="col" className={th}>
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {lista.data
            ? lista.data.itens.map((a) => (
                <tr key={a.id} className={trClicavel}>
                  <td className={cn(td, soMedio, "text-rotulo whitespace-nowrap text-suave tabular-nums")}>{formatarData(a.inicio)}</td>
                  <td className={cn(td, soMedio, "text-rotulo whitespace-nowrap tabular-nums")}>
                    {ROTULO_TURNO[a.turno]} · {formatarHora(a.inicio)}–{formatarHora(a.fim)}
                  </td>
                  <td className={td}>
                    {/* A partir de `sm` curso e tipo dividem a linha; no celular o tipo desce para a linha dos detalhes. */}
                    <div className="flex flex-wrap items-center gap-x-1.5">
                      <span className="font-semibold">{a.curso ?? <Badge variante="neutro">Institucional</Badge>}</span>
                      <span className="max-sm:hidden">
                        · {ROTULO_TIPO_ATIVIDADE[a.tipo]}
                        {a.avulsa && <span className="text-suave"> · avulsa</span>}
                      </span>
                    </div>
                    <div className="text-rotulo text-suave tabular-nums sm:hidden">
                      {ROTULO_TIPO_ATIVIDADE[a.tipo]}
                      {a.avulsa && " · avulsa"} · {formatarData(a.inicio)} · {ROTULO_TURNO[a.turno]} · {formatarHora(a.inicio)}–{formatarHora(a.fim)} · {a.profissional}
                    </div>
                  </td>
                  <td className={cn(td, "hidden text-rotulo whitespace-nowrap lg:table-cell")}>{formatarDuracao(a.duracaoMinutos)}</td>
                  <td className={cn(td, "hidden text-rotulo lg:table-cell")}>{a.profissional}</td>
                  <td className={cn(td, "text-right max-sm:pl-0")}>
                    <button
                      type="button"
                      onClick={() => aoAbrir(a.id)}
                      aria-label={`Abrir atividade de ${formatarData(a.inicio)}: ${ROTULO_TIPO_ATIVIDADE[a.tipo]}, ${a.curso ?? "institucional"}`}
                      className="inline-flex items-center gap-1 rounded-sm text-xs font-semibold whitespace-nowrap text-primaria after:absolute after:inset-0 after:content-['']"
                    >
                      <span className="hidden lg:inline">Abrir</span>
                      <ChevronRight aria-hidden="true" className="size-3.5" />
                    </button>
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
          {/* Página da URL, não da resposta: com placeholder `lista.data.pagina` ainda é a anterior e o ajuste "além do fim" voltaria à última. */}
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
