import { Table } from "../../../components/ui/Table";
import { formatarInteiro, formatarPercentual } from "../../../lib/format";
import type { Producao } from "../../../lib/types";
import { minutosComSinal } from "./TabelaCategorias";

/** Percentual sempre com a base (o cabeçalho da coluna explica o que ela conta); sem base, o motivo. */
const textoTaxa = (t: Producao["taxaAusencia"]) => (t.valor == null ? (t.motivo ?? "—") : `${formatarPercentual(t.valor, 1)} (${formatarInteiro(t.numerador)} de ${formatarInteiro(t.denominador)})`);

/** Nomes dos profissionais, nunca alunos. */
export function ComparacaoProfissionais({ linhas }: { linhas: NonNullable<Producao["porProfissional"]> }) {
  const total = (l: (typeof linhas)[number], chave: string) => l.categorias.find((c) => c.chave === chave)?.total;
  return (
    <Table caption="Comparação entre profissionais">
      <thead>
        <tr className="border-b border-borda text-xs text-suave">
          {["Profissional", "Atendimentos realizados", "Ausências", "Taxa de ausência", "Capacidade", "Ociosidade", "Pendentes"].map((r, i) => (
            <th key={r} scope="col" className={i === 0 ? "py-1 font-semibold" : "py-1 pl-3 text-right font-semibold"}>
              {r}
              {r === "Taxa de ausência" && <span className="block font-normal">faltas ÷ (individuais + faltas)</span>}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {linhas.map((l) => (
          <tr key={l.profissionalId} className="border-b border-borda">
            <th scope="row" className="py-1 pr-2 font-normal">{l.profissional}</th>
            <td className="py-1 pl-3 text-right tabular-nums">{formatarInteiro(total(l, "AtendimentosRealizados")?.ocorrencias)}</td>
            <td className="py-1 pl-3 text-right tabular-nums">{formatarInteiro(total(l, "Ausencias")?.ocorrencias)}</td>
            <td className="py-1 pl-3 text-right whitespace-nowrap">{textoTaxa(l.taxaAusencia)}</td>
            <td className="py-1 pl-3 text-right tabular-nums">{l.tempo ? minutosComSinal(l.tempo.capacidade) : "—"}</td>
            <td className="py-1 pl-3 text-right tabular-nums">{l.tempo ? minutosComSinal(l.tempo.ociosidade) : "—"}</td>
            <td className="py-1 pl-3 text-right tabular-nums">{formatarInteiro(l.pendentes)}</td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
