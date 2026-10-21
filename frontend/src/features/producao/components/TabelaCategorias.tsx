import { Table } from "../../../components/ui/Table";
import { formatarDuracao, formatarInteiro } from "../../../lib/format";
import type { CategoriaProducao } from "../../../lib/types";

/** Minutos com sinal: ajustes e ociosidade inconsistente podem ser negativos. Hífen-menos, o mesmo do Intl nos percentuais e ocorrências. */
export function minutosComSinal(minutos: number): string {
  return minutos < 0 ? `-${formatarDuracao(-minutos)}` : formatarDuracao(minutos);
}

const ocorrencias = (v: number | null) => (v == null ? "—" : formatarInteiro(v));
const celula = "py-1 pl-3 text-right tabular-nums";

/** Ocorrências e tempo em colunas separadas: nunca se somam entre si. Ociosidade só tem tempo ("—" nas ocorrências). */
export function TabelaCategorias({ categorias }: { categorias: CategoriaProducao[] }) {
  return (
    <Table caption="Produção por categoria">
      <thead>
        <tr className="text-xs text-suave">
          <th scope="col" rowSpan={2} className="py-1 align-bottom font-semibold">Categoria</th>
          <th scope="colgroup" colSpan={3} className="border-b border-borda py-1 text-center font-semibold">Ocorrências</th>
          <th scope="colgroup" colSpan={3} className="border-b border-borda py-1 text-center font-semibold">Tempo</th>
        </tr>
        <tr className="border-b border-borda text-xs text-suave">
          {["Registrado", "Ajustes", "Total", "Registrado", "Ajustes", "Total"].map((r, i) => (
            <th key={i} scope="col" className="py-1 pl-3 text-right font-semibold">{r}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {categorias.map((c) => (
          <tr key={c.chave} className="border-b border-borda">
            <th scope="row" className="py-1 pr-2 font-normal">{c.rotulo}</th>
            <td className={celula}>{ocorrencias(c.registrado.ocorrencias)}</td>
            <td className={celula}>{ocorrencias(c.ajustes.ocorrencias)}</td>
            <td className={`${celula} font-semibold`}>{ocorrencias(c.total.ocorrencias)}</td>
            <td className={celula}>{minutosComSinal(c.registrado.minutos)}</td>
            <td className={celula}>{minutosComSinal(c.ajustes.minutos)}</td>
            <td className={`${celula} font-semibold`}>{minutosComSinal(c.total.minutos)}</td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
