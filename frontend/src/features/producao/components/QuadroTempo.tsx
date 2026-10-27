import type { ReactNode } from "react";
import { Alert } from "../../../components/ui/Alert";
import { formatarDataCivil, formatarInteiro } from "../../../lib/format";
import type { TempoProducao } from "../../../lib/types";
import { minutosComSinal } from "./TabelaCategorias";

/** Capacidade e uso do tempo na escala, mais a taxa de ausência. Ociosidade negativa não é escondida: vem como "Inconsistente". */
export function QuadroTempo({ tempo, pendentes, taxaAusencia }: { tempo: TempoProducao; pendentes: number | null; taxaAusencia: ReactNode }) {
  const itens: [string, string][] = [
    ["Capacidade", minutosComSinal(tempo.capacidade)],
    ["Produção na escala", minutosComSinal(tempo.producaoNaEscala)],
    ["Ausências na escala", minutosComSinal(tempo.ausenciasNaEscala)],
    ["Pendentes na escala", minutosComSinal(tempo.pendentesNaEscala)],
    ["Ociosidade", minutosComSinal(tempo.ociosidade)],
    ["Tempo extra", minutosComSinal(tempo.tempoExtra)],
  ];
  return (
    <div className="flex flex-col gap-2">
      {tempo.inconsistente && (
        <Alert variante="urgente">
          <strong>Inconsistente:</strong> o tempo registrado passa da capacidade
          {tempo.diasInconsistentes.length > 0 && ` em ${tempo.diasInconsistentes.map(formatarDataCivil).join(", ")}`}. Confira escala, bloqueios e lançamentos.
        </Alert>
      )}
      <dl className="m-0 grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-4">
        {itens.map(([rotulo, valor]) => (
          <div key={rotulo}>
            <dt className="text-xs text-suave">{rotulo}</dt>
            <dd className="m-0 font-semibold tabular-nums">{valor}</dd>
          </div>
        ))}
        {/* A taxa ocupa duas colunas: o valor vem com a base (ou o motivo) por extenso. */}
        <div className="col-span-2">
          <dt className="text-xs text-suave">Taxa de ausência</dt>
          <dd className="m-0">{taxaAusencia}</dd>
        </div>
      </dl>
      {pendentes != null && pendentes > 0 && (
        <p className="m-0 text-xs text-suave">
          {formatarInteiro(pendentes)} {pendentes === 1 ? "compromisso aguarda" : "compromissos aguardam"} classificação (registro ou realização) e {pendentes === 1 ? "está" : "estão"} fora da produção.
        </p>
      )}
    </div>
  );
}
