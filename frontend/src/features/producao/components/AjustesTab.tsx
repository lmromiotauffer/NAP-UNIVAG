import { Plus, SlidersHorizontal } from "lucide-react";
import { useState } from "react";
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
import { formatarDataCivil, formatarDataHora, formatarInteiro } from "../../../lib/format";
import { useAjustesProducao } from "../hooks/useProducao";
import { AjusteForm } from "./AjusteForm";
import { minutosComSinal } from "./TabelaCategorias";

const TAMANHO_PAGINA = 20;

/** Ajustes do período (só coordenação). Ajuste soma à produção, mas não entra na capacidade. */
export function AjustesTab({ inicio, fim, profissionalId }: { inicio: string; fim: string; profissionalId?: string }) {
  const consulta = useAjustesProducao({ inicio, fim, profissionalId });
  const [novo, setNovo] = useState(false);
  // Paginação no cliente: a lista é limitada pelo período. O ProducaoPage remonta a aba ao mudar filtro, voltando à página 1.
  const [pagina, setPagina] = useState(1);
  const toast = useToast();
  // A chave da consulta pode mudar sem remontar a aba (mesmo início, fim diferente): a lista anterior esmaece até a resposta chegar.
  const ocupado = consulta.isPlaceholderData || undefined;
  return (
    <div className="flex flex-col gap-3 md:min-h-0 md:flex-1">
      <div className="flex justify-end">
        <Button onClick={() => setNovo(true)} className="rounded-full">
          <Plus aria-hidden="true" className="size-4" />
          Novo ajuste
        </Button>
      </div>
      {consulta.isPending && (
        <Carregando rotulo="Carregando ajustes">
          <Skeleton className="h-32 rounded-cartao" />
        </Carregando>
      )}
      {consulta.isError && <ErrorState erro={consulta.error} aoTentarNovamente={() => void consulta.refetch()} />}
      {/* Vazio dentro da mesma moldura da lista, como nas listagens da administração. */}
      {consulta.data && consulta.data.length === 0 && (
        <section aria-label="Ajustes do período" aria-busy={ocupado} className={cn(classeMolduraRolavel, "transition-opacity duration-150", ocupado && "opacity-60")}>
          <EmptyState icone={<SlidersHorizontal />} titulo="Nenhum ajuste no período" />
        </section>
      )}
      {consulta.data && consulta.data.length > 0 && (
        <section aria-label="Ajustes do período" aria-busy={ocupado} className={cn(classeMolduraRolavel, "transition-opacity duration-150", ocupado && "opacity-60")}>
          <Table caption="Ajustes do período">
            <thead>
              <tr className="border-b border-borda">
                {["Data", "Profissional", "Categoria", "Ocorrências", "Minutos", "Justificativa", "Autor"].map((r, i) => (
                  <th key={r} scope="col" className={`${th} ${i === 3 || i === 4 ? "text-right" : ""}`}>
                    {r}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {consulta.data.slice((pagina - 1) * TAMANHO_PAGINA, pagina * TAMANHO_PAGINA).map((a) => (
                <tr key={a.id} className="border-b border-borda align-top">
                  <td className={td}>{formatarDataCivil(a.data)}</td>
                  <td className={td}>{a.profissional}</td>
                  <td className={td}>{a.rotulo}</td>
                  <td className={`${td} text-right tabular-nums`}>{a.ocorrencias == null ? "—" : formatarInteiro(a.ocorrencias)}</td>
                  <td className={`${td} text-right tabular-nums`}>{a.minutos == null ? "—" : minutosComSinal(a.minutos)}</td>
                  <td className={td}>{a.justificativa}</td>
                  <td className={td}>
                    {a.autor}
                    <span className="block text-xs text-suave">{formatarDataHora(a.em)}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
          <div className="px-4 md:mt-auto">
            <Paginacao pagina={pagina} tamanho={TAMANHO_PAGINA} total={consulta.data.length} aoMudar={setPagina} />
          </div>
        </section>
      )}
      <Modal aberto={novo} aoFechar={() => setNovo(false)} titulo="Novo ajuste">
        <AjusteForm
          profissionalPadrao={profissionalId}
          aoConcluir={() => {
            toast.sucesso("Ajuste registrado.");
            setNovo(false);
          }}
          aoCancelar={() => setNovo(false)}
        />
      </Modal>
    </div>
  );
}
