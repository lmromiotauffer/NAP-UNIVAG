import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "../../../components/ui/Button";
import { Select } from "../../../components/ui/Select";
import { formatarDataCivil, somarDias } from "../../../lib/format";
import type { Producao } from "../../../lib/types";

export type TipoPeriodo = Producao["periodo"]["tipo"];
export const ROTULO_PERIODO: Record<TipoPeriodo, string> = { Semana: "Semana", Mes: "Mês", Semestre: "Semestre", Ano: "Ano" };
const MESES_POR_PERIODO = { Mes: 1, Semestre: 6, Ano: 12 } as const;

/** Referência do período vizinho: semana ±7 dias; mês, semestre e ano pelo dia 1 do mês (evita estourar o dia 31). */
export function deslocarReferencia(tipo: TipoPeriodo, referencia: string, sentido: 1 | -1): string {
  if (tipo === "Semana") return somarDias(referencia, 7 * sentido);
  const total = Number(referencia.slice(0, 4)) * 12 + Number(referencia.slice(5, 7)) - 1 + MESES_POR_PERIODO[tipo] * sentido;
  return `${Math.floor(total / 12)}-${String((total % 12) + 1).padStart(2, "0")}-01`;
}

export function SeletorPeriodo({
  tipo,
  referencia,
  hoje,
  periodo,
  aoMudar,
}: {
  tipo: TipoPeriodo;
  referencia: string;
  hoje: string;
  periodo?: Producao["periodo"];
  aoMudar: (tipo: TipoPeriodo, referencia: string) => void;
}) {
  const proxima = deslocarReferencia(tipo, referencia, 1);
  // A referência deslocada não é o início do próximo período: com a resposta, vale o fim do período atual.
  const semProximo = periodo ? periodo.fim >= hoje : proxima > hoje;
  return (
    <div role="group" aria-label="Período" className="flex flex-wrap items-center gap-2">
      <label htmlFor="producao-tipo" className="sr-only">
        Tipo de período
      </label>
      <Select id="producao-tipo" value={tipo} onChange={(e) => aoMudar(e.target.value as TipoPeriodo, referencia)} className="h-9 w-auto min-w-[7rem] rounded-full py-0 text-rotulo">
        {(Object.keys(ROTULO_PERIODO) as TipoPeriodo[]).map((t) => (
          <option key={t} value={t}>
            {ROTULO_PERIODO[t]}
          </option>
        ))}
      </Select>
      <div className="flex items-center gap-1 rounded-full bg-bloco px-1 text-rotulo text-bloco-texto">
        <Button tamanho="icone" variante="fantasma" aria-label="Período anterior" className="size-8 rounded-full text-bloco-texto hover:bg-bloco-texto/10 focus-visible:outline-bloco-texto" onClick={() => aoMudar(tipo, deslocarReferencia(tipo, referencia, -1))}>
          <ChevronLeft aria-hidden="true" className="size-4" />
        </Button>
        <span aria-live="polite" className="px-1 text-center sm:whitespace-nowrap">
          {periodo ? `${formatarDataCivil(periodo.inicio)} a ${formatarDataCivil(periodo.fim)}` : formatarDataCivil(referencia)}
        </span>
        <Button tamanho="icone" variante="fantasma" aria-label="Próximo período" disabled={semProximo} className="size-8 rounded-full text-bloco-texto hover:bg-bloco-texto/10 focus-visible:outline-bloco-texto" onClick={() => aoMudar(tipo, proxima)}>
          <ChevronRight aria-hidden="true" className="size-4" />
        </Button>
      </div>
    </div>
  );
}
