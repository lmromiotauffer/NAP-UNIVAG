import { Table } from "../../../components/ui/Table";
import { formatarDuracao, formatarInteiro } from "../../../lib/format";
import type { ProducaoProfissional } from "../../../lib/types";

const th = "py-1.5 text-xs font-semibold text-suave";
// Recuo à esquerda das colunas numéricas: sem ele os cabeçalhos se encostam no celular.
const num = "pl-3 text-right tabular-nums";

/** Produção da semana por profissional. Contagens e tempo em colunas separadas; faltas não somam aos realizados. */
export function TabelaPorProfissional({ dados }: { dados: ProducaoProfissional[] }) {
  if (dados.length === 0) return <p className="m-0 text-sm text-suave">Nenhum atendimento registrado na semana.</p>;
  return (
    <Table caption="Produção por profissional na semana">
      <thead>
        <tr className="border-b border-borda">
          <th scope="col" className={th}>Profissional</th>
          <th scope="col" className={`${th} ${num}`}>Realizados</th>
          <th scope="col" className={`${th} ${num}`}>Faltas</th>
          <th scope="col" className={`${th} ${num}`}>Tempo realizado</th>
        </tr>
      </thead>
      <tbody>
        {dados.map((p) => (
          <tr key={p.id} className="border-b border-borda">
            <th scope="row" className="py-1.5 font-medium">{p.nome}</th>
            <td className={`py-1.5 ${num}`}>{formatarInteiro(p.realizados)}</td>
            <td className={`py-1.5 ${num}`}>{formatarInteiro(p.faltas)}</td>
            <td className={`py-1.5 ${num}`}>{formatarDuracao(p.minutos)}</td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
