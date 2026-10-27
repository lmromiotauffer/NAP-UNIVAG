import { CalendarCheck } from "lucide-react";
import { useState } from "react";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Modal } from "../../../components/ui/Modal";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Carregando } from "../../../components/ui/Carregando";
import { Skeleton } from "../../../components/ui/Skeleton";
import { classeMolduraRolavel, Table, th, td } from "../../../components/ui/Table";
import { useToast } from "../../../components/ui/Toast";
import { cn } from "../../../lib/cn";
import { formatarDataCivil, formatarDataHora, formatarInteiro, somarDias } from "../../../lib/format";
import type { SemanaProducao } from "../../../lib/types";
import { useSemanasProducao } from "../hooks/useProducao";
import { FecharSemanaDialog } from "./FecharSemanaDialog";
import { ReabrirSemanaDialog } from "./ReabrirSemanaDialog";

const TAMANHO_PAGINA = 20;

/** Semanas do período por profissional. Fechar só vale para semana já encerrada (domingo antes de hoje); reabrir é da coordenação. */
export function SemanasTab({ inicio, fim, profissionalId, hoje, podeReabrir }: { inicio: string; fim: string; profissionalId?: string; hoje: string; podeReabrir: boolean }) {
  const consulta = useSemanasProducao({ inicio, fim, profissionalId });
  const [fechando, setFechando] = useState<SemanaProducao | null>(null);
  // Paginação no cliente: a lista é limitada pelo período (semanas × equipe). O ProducaoPage remonta a aba ao mudar filtro, voltando à página 1.
  const [pagina, setPagina] = useState(1);
  const [reabrindo, setReabrindo] = useState<SemanaProducao | null>(null);
  const toast = useToast();
  // A chave da consulta pode mudar sem remontar a aba (mesmo início, fim diferente): a lista anterior esmaece até a resposta chegar.
  const ocupado = consulta.isPlaceholderData || undefined;
  return (
    <div className="flex flex-col gap-3 md:min-h-0 md:flex-1">
      {consulta.isPending && (
        <Carregando rotulo="Carregando semanas">
          <Skeleton className="h-32 rounded-cartao" />
        </Carregando>
      )}
      {consulta.isError && <ErrorState erro={consulta.error} aoTentarNovamente={() => void consulta.refetch()} />}
      {/* Vazio dentro da mesma moldura da lista, como nas listagens da administração. */}
      {consulta.data && consulta.data.length === 0 && (
        <section aria-label="Semanas do período" aria-busy={ocupado} className={cn(classeMolduraRolavel, "transition-opacity duration-150", ocupado && "opacity-60")}>
          <EmptyState icone={<CalendarCheck />} titulo="Nenhuma semana no período" />
        </section>
      )}
      {consulta.data && consulta.data.length > 0 && (
        <section aria-label="Semanas do período" aria-busy={ocupado} className={cn(classeMolduraRolavel, "transition-opacity duration-150", ocupado && "opacity-60")}>
          <Table caption="Semanas do período">
            <thead>
              <tr className="border-b border-borda">
                {["Semana", "Profissional", "Situação", "Pendências", "Histórico", "Ações"].map((r) => (
                  <th key={r} scope="col" className={cn(th, r === "Pendências" && "text-right")}>
                    {r === "Ações" ? <span className="sr-only">{r}</span> : r}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {consulta.data.slice((pagina - 1) * TAMANHO_PAGINA, pagina * TAMANHO_PAGINA).map((s) => {
                const encerrada = somarDias(s.semana, 6) < hoje;
                const nome = `${formatarDataCivil(s.semana)}, ${s.profissional}`;
                return (
                  <tr key={`${s.profissionalId}-${s.semana}`} className="border-b border-borda align-top">
                    {/* No celular a tabela rola na horizontal: o intervalo fica numa linha em vez de dobrar a altura. */}
                    <td className={`${td} whitespace-nowrap`}>
                      {formatarDataCivil(s.semana)} a {formatarDataCivil(somarDias(s.semana, 6))}
                    </td>
                    <td className={td}>{s.profissional}</td>
                    <td className={td}>
                      <Badge variante={s.fechada ? "realizado" : "neutro"}>{s.fechada ? "Fechada" : "Aberta"}</Badge>
                    </td>
                    <td className={`${td} text-right tabular-nums`}>{formatarInteiro(s.pendentes)}</td>
                    <td className={td}>
                      {s.eventos.length === 0 ? (
                        "—"
                      ) : (
                        <ul className="m-0 flex list-none flex-col gap-0.5 p-0 text-xs">
                          {s.eventos.map((e, i) => (
                            <li key={i}>
                              {e.tipo === "Fechamento" ? "Fechada" : "Reaberta"} por {e.autor} em {formatarDataHora(e.em)}
                              {e.justificativa && <span className="text-suave"> · {e.justificativa}</span>}
                            </li>
                          ))}
                        </ul>
                      )}
                    </td>
                    <td className={td}>
                      {!s.fechada && encerrada && (
                        <Button tamanho="sm" variante="secundaria" className="pointer-fine:-my-1" onClick={() => setFechando(s)} aria-label={`Fechar semana de ${nome}`}>
                          Fechar semana
                        </Button>
                      )}
                      {s.fechada && podeReabrir && (
                        <Button tamanho="sm" variante="secundaria" className="pointer-fine:-my-1" onClick={() => setReabrindo(s)} aria-label={`Reabrir semana de ${nome}`}>
                          Reabrir
                        </Button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </Table>
          <div className="px-4 md:mt-auto">
            <Paginacao pagina={pagina} tamanho={TAMANHO_PAGINA} total={consulta.data.length} aoMudar={setPagina} />
          </div>
        </section>
      )}
      {fechando && (
        <FecharSemanaDialog
          semana={fechando}
          aoConcluir={() => {
            toast.sucesso("Semana fechada.");
            setFechando(null);
          }}
          aoCancelar={() => setFechando(null)}
        />
      )}
      <Modal aberto={!!reabrindo} aoFechar={() => setReabrindo(null)} titulo="Reabrir semana">
        {reabrindo && (
          <ReabrirSemanaDialog
            semana={reabrindo}
            aoConcluir={() => {
              toast.sucesso("Semana reaberta.");
              setReabrindo(null);
            }}
            aoCancelar={() => setReabrindo(null)}
          />
        )}
      </Modal>
    </div>
  );
}
