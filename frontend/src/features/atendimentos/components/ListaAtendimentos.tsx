import { ChevronRight } from "lucide-react";
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
import { estiloAtendimento, ROTULO_TIPO_ATENDIMENTO } from "../../agenda/components/estiloCompromisso";
import { useListaAtendimentos } from "../hooks/useAtendimentos";
import type { FiltroAtendimentos } from "../services/atendimentoService";

// No celular a linha fica compacta (padrão de lista): data, aluno e estado sobem para a célula do tipo.

/** Tabela paginada de atendimentos (sem observações privadas: a listagem nunca as traz). */
export function ListaAtendimentos({
  filtro,
  comFiltro,
  semAluno,
  vazio,
  aoMudarPagina,
  aoAbrir,
}: {
  filtro: FiltroAtendimentos;
  comFiltro: boolean;
  /** Na ficha do aluno a coluna Aluno é redundante. */
  semAluno?: boolean;
  /** Estado vazio sem filtro (e com ação), ex.: "Registrar atendimento". */
  vazio: { titulo: string; descricao: string; acao?: ReactNode; acaoFiltro?: ReactNode; icone?: ReactNode };
  aoMudarPagina: (pagina: number) => void;
  aoAbrir: (id: string) => void;
}) {
  const lista = useListaAtendimentos(filtro);
  // Esqueleto espelha as colunas visíveis em cada largura.
  const colunas = [soMedio, ...(semAluno ? [] : [soMedio]), "", "hidden lg:table-cell", soMedio, ""];

  if (lista.isError && !lista.data)
    return (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  if (lista.data && lista.data.total === 0 && !lista.isPlaceholderData)
    return comFiltro ? (
      <EmptyState icone={vazio.icone} titulo="Nenhum atendimento corresponde aos filtros" descricao="Revise os filtros selecionados." acao={vazio.acaoFiltro} />
    ) : (
      <EmptyState icone={vazio.icone} titulo={vazio.titulo} descricao={vazio.descricao} acao={vazio.acao} />
    );
  if (lista.data && lista.data.itens.length === 0 && !lista.isPlaceholderData)
    // Link antigo com `pagina` além da última: sem a Paginacao montada, este botão é o único caminho de volta.
    return (
      <EmptyState
        icone={vazio.icone}
        titulo="Esta página não tem atendimentos"
        descricao={`A lista tem ${lista.data.total} ${lista.data.total === 1 ? "atendimento" : "atendimentos"}.`}
        acao={
          <Button variante="secundaria" tamanho="sm" onClick={() => aoMudarPagina(1)}>
            Voltar à primeira página
          </Button>
        }
      />
    );

  return (
    <>
      <Table caption="Atendimentos" aria-busy={lista.isPending || lista.isPlaceholderData} className={cn(lista.isPlaceholderData && "opacity-60")}>
        <thead>
          <tr className="border-b border-borda">
            <th scope="col" className={cn(th, soMedio)}>Data</th>
            {!semAluno && <th scope="col" className={cn(th, soMedio)}>Aluno</th>}
            <th scope="col" className={th}>
              {/* No celular a célula junta aluno, tipo e data: o cabeçalho acompanha. */}
              <span className="sm:hidden">Atendimento</span>
              <span className="max-sm:hidden">Tipo · demanda</span>
            </th>
            <th scope="col" className={cn(th, "hidden lg:table-cell")}>Profissional · duração</th>
            <th scope="col" className={cn(th, soMedio)}>Estado</th>
            <th scope="col" className={th}>
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {lista.data
            ? lista.data.itens.map((a) => {
                const estilo = estiloAtendimento(a.estado);
                return (
                  <tr key={a.id} className={trClicavel}>
                    <td className={cn(td, soMedio, "text-rotulo whitespace-nowrap text-suave tabular-nums")}>
                      {formatarData(a.inicio)} · {formatarHora(a.inicio)}
                    </td>
                    {!semAluno && <td className={cn(td, soMedio, "font-semibold")}>{a.aluno}</td>}
                    <td className={cn(td, "sm:leading-tight")}>
                      {/* Celular: aluno (ou tipo) e estado na primeira linha; tipo e data na segunda. */}
                      <div className="flex flex-wrap items-center gap-x-1.5 gap-y-0.5 sm:hidden">
                        <span className="font-semibold">
                          {semAluno ? ROTULO_TIPO_ATENDIMENTO[a.tipo] : a.aluno}
                          {semAluno && a.avulso && <span className="font-normal text-suave"> · avulso</span>}
                        </span>
                        <Badge variante={estilo.variante} className="whitespace-nowrap">
                          {estilo.rotuloEstado}
                        </Badge>
                      </div>
                      <div className="text-rotulo text-suave tabular-nums sm:hidden">
                        {!semAluno && `${ROTULO_TIPO_ATENDIMENTO[a.tipo]}${a.avulso ? " · avulso" : ""} · `}
                        {formatarData(a.inicio)} · {formatarHora(a.inicio)}
                      </div>
                      <div className="max-sm:hidden">
                        {ROTULO_TIPO_ATENDIMENTO[a.tipo]}
                        {a.avulso && <span className="text-suave"> · avulso</span>}
                      </div>
                      <div className="hidden max-w-[28ch] truncate text-rotulo text-suave sm:block">{a.demandaPrincipal ?? "—"}</div>
                    </td>
                    <td className={cn(td, "hidden text-rotulo whitespace-nowrap lg:table-cell")}>
                      {a.profissional} <span className="text-suave">· {formatarDuracao(a.duracaoMinutos)}</span>
                    </td>
                    <td className={cn(td, soMedio)}>
                      <Badge variante={estilo.variante} className="whitespace-nowrap">
                        {estilo.rotuloEstado}
                      </Badge>
                    </td>
                    <td className={cn(td, "text-right max-sm:pl-0")}>
                      <button
                        type="button"
                        onClick={() => aoAbrir(a.id)}
                        aria-label={`Abrir atendimento de ${a.aluno} em ${formatarData(a.inicio)}`}
                        className="inline-flex items-center gap-1 rounded-sm text-xs font-semibold whitespace-nowrap text-primaria after:absolute after:inset-0 after:content-['']"
                      >
                        {/* O ::after estende o botão à linha toda: o alvo de toque deixa de ser só a seta. */}
                        <span className="hidden lg:inline">Abrir</span>
                        <ChevronRight aria-hidden="true" className="size-3.5" />
                      </button>
                    </td>
                  </tr>
                );
              })
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
