import { cn } from "../../lib/cn";
import { formatarInteiro } from "../../lib/format";
import { TabelaValores } from "./GraficoBarras";

/** `classe`: classe de token do traço (ex.: "stroke-info"); a legenda repete o nome e o valor em texto. */
export type FatiaAnel = { rotulo: string; valor: number; classe: string; classeLegenda: string };

const LARGURA_VIEWBOX = 120;
const CENTRO = LARGURA_VIEWBOX / 2;
const RAIO = 46;
const ESPESSURA = 14;
const VAO = 1.5;
const CIRCUNFERENCIA = 2 * Math.PI * RAIO;

export function GraficoAnel({ titulo, descricao, dados, unidade, rotuloItem, className }: { titulo: string; descricao: string; dados: FatiaAnel[]; unidade: string; rotuloItem?: string; className?: string }) {
  const total = dados.reduce((s, d) => s + d.valor, 0);
  let acumulado = 0;
  return (
    <div className={cn("flex flex-wrap items-center gap-4", className)}>
      {/* key pelos valores: a cada recálculo o anel remonta e se traça de novo. */}
      <svg key={dados.map((d) => d.valor).join()} role="img" aria-label={titulo} viewBox={`0 0 ${LARGURA_VIEWBOX} ${LARGURA_VIEWBOX}`} className="block h-auto w-24 flex-none">
        <desc>{descricao}</desc>
        <g aria-hidden="true" transform={`rotate(-90 ${CENTRO} ${CENTRO})`}>
          <circle cx={CENTRO} cy={CENTRO} r={RAIO} fill="none" strokeWidth={ESPESSURA} className="stroke-realce" />
          {total > 0 &&
            dados.map((d) => {
              const comprimento = (d.valor / total) * CIRCUNFERENCIA;
              const inicio = acumulado;
              acumulado += comprimento;
              if (d.valor === 0) return null;
              const visivel = Math.max(0.5, comprimento - (dados.length > 1 ? VAO : 0));
              return (
                <circle
                  key={d.rotulo}
                  cx={CENTRO}
                  cy={CENTRO}
                  r={RAIO}
                  fill="none"
                  strokeWidth={ESPESSURA}
                  strokeDasharray={`${visivel} ${CIRCUNFERENCIA - visivel}`}
                  strokeDashoffset={-inicio}
                  className={cn("animate-tracar motion-reduce:animate-none", d.classe)}
                  // Cada fatia começa quando a anterior termina, no tempo proporcional ao seu arco: o anel se traça como uma linha só.
                  style={{ animationDelay: `${(inicio / CIRCUNFERENCIA) * 500}ms`, animationDuration: `${(comprimento / CIRCUNFERENCIA) * 500}ms` }}
                />
              );
            })}
        </g>
        <text aria-hidden="true" x={CENTRO} y={CENTRO + 2} textAnchor="middle" fontSize="22" fontWeight="900" className="fill-texto tabular-nums">
          {formatarInteiro(total)}
        </text>
        <text aria-hidden="true" x={CENTRO} y={CENTRO + 17} textAnchor="middle" fontSize="9.5" className="fill-suave">
          {unidade}
        </text>
      </svg>
      <ul aria-hidden="true" className="m-0 flex list-none flex-col gap-1 p-0 text-xs text-suave">
        {dados.map((d) => (
          <li key={d.rotulo} className="flex items-center gap-1.5">
            <span className={cn("size-2 flex-none rounded-xs", d.classeLegenda)} />
            {d.rotulo} <strong className="text-texto tabular-nums">{formatarInteiro(d.valor)}</strong>
          </li>
        ))}
      </ul>
      <TabelaValores titulo={titulo} dados={dados} unidade={unidade} rotuloItem={rotuloItem} />
    </div>
  );
}
