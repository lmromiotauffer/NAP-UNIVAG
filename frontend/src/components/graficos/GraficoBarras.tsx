import { useId } from "react";
import { cn } from "../../lib/cn";
import { formatarInteiro } from "../../lib/format";

export type DadoGrafico = { rotulo: string; valor: number | null; parcial?: boolean };

const LARGURA_VIEWBOX = 320;
const ALTURA_VIEWBOX = 190;
const MARGEM = { topo: 16, base: 30, lateral: 4 };
const FRACAO_BARRA = 0.62;
// Horizontal: uma linha por item, com o rótulo à esquerda e o valor à direita.
const ALTURA_LINHA = 20;
const LARGURA_ROTULO = 112;
const LARGURA_VALOR = 34;
const ESPESSURA_LINHA = 9;

const rotuloLinha = (d: DadoGrafico) => (d.parcial ? `${d.rotulo} (parcial)` : d.rotulo);

/** Tabela com os mesmos valores do gráfico: `sr-only` ao lado do SVG ou visível em "Detalhes". */
export function TabelaValores({ titulo, dados, unidade, rotuloItem = "Item", visivel }: { titulo: string; dados: DadoGrafico[]; unidade: string; rotuloItem?: string; visivel?: boolean }) {
  // Oculta, a tabela fica dentro de um div sr-only: tabela não encolhe a 1px e, sozinha, alargaria a página.
  const tabela = (
    <table className={cn(visivel && "w-full border-collapse text-left text-sm")}>
      <caption className={cn(visivel && "sr-only")}>{titulo}</caption>
      <thead>
        <tr className={cn(visivel && "border-b border-borda text-xs text-suave")}>
          <th scope="col" className="py-1.5 font-semibold">{rotuloItem}</th>
          <th scope="col" className="py-1.5 text-right font-semibold">{unidade}</th>
        </tr>
      </thead>
      <tbody>
        {dados.map((d) => (
          <tr key={d.rotulo} className={cn(visivel && "border-b border-borda")}>
            <th scope="row" className="py-1.5 font-normal">{rotuloLinha(d)}</th>
            <td className="py-1.5 text-right tabular-nums">{formatarInteiro(d.valor)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
  return visivel ? tabela : <div className="sr-only">{tabela}</div>;
}

/** Barras em SVG próprio. `null` não vira zero: sem barra e com "—". Cores só por classes de token. */
export function GraficoBarras({
  titulo,
  descricao,
  dados,
  unidade,
  orientacao,
  rotuloItem,
  className,
  tabelaVisivel,
}: {
  titulo: string;
  descricao: string;
  dados: DadoGrafico[];
  unidade: string;
  orientacao: "vertical" | "horizontal";
  rotuloItem?: string;
  className?: string;
  /** Mostra a tabela de valores abaixo do gráfico (telas de detalhe); por padrão ela fica só para leitores de tela. */
  tabelaVisivel?: boolean;
}) {
  // O id entra em url(#…): sem os ":" que o useId gera.
  const hachura = `hachura-${useId().replace(/\W/g, "")}`;
  const maximo = Math.max(1, ...dados.map((d) => d.valor ?? 0));
  const altura = orientacao === "vertical" ? ALTURA_VIEWBOX : Math.max(ALTURA_LINHA, dados.length * ALTURA_LINHA);

  return (
    // O viewBox tem 320 de largura: o teto evita que o texto do SVG cresça demais em cartões largos.
    <div className={cn(tabelaVisivel && "flex flex-col gap-3", className)}>
      {/* key pelos valores: quando o filtro recalcula os números, o gráfico remonta e as barras crescem de novo. */}
      <svg key={dados.map((d) => d.valor).join()} role="img" aria-label={titulo} viewBox={`0 0 ${LARGURA_VIEWBOX} ${altura}`} className="mx-auto block h-auto w-full max-w-[440px] overflow-visible">
        <desc>{descricao}</desc>
        <defs>
          <pattern id={hachura} width="5" height="5" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
            <line x1="0" y1="0" x2="0" y2="5" strokeWidth="2.5" className="stroke-primaria" />
          </pattern>
        </defs>
        {orientacao === "vertical" ? <Verticais dados={dados} maximo={maximo} hachura={hachura} /> : <Horizontais dados={dados} maximo={maximo} />}
      </svg>
      <TabelaValores titulo={titulo} dados={dados} unidade={unidade} rotuloItem={rotuloItem} visivel={tabelaVisivel} />
    </div>
  );
}

function Verticais({ dados, maximo, hachura }: { dados: DadoGrafico[]; maximo: number; hachura: string }) {
  const fatia = (LARGURA_VIEWBOX - 2 * MARGEM.lateral) / Math.max(1, dados.length);
  const largura = fatia * FRACAO_BARRA;
  const base = ALTURA_VIEWBOX - MARGEM.base;
  const util = base - MARGEM.topo;
  return (
    <g aria-hidden="true">
      <line x1={0} x2={LARGURA_VIEWBOX} y1={base} y2={base} strokeWidth="1" className="stroke-borda" />
      {dados.map((d, i) => {
        const centro = MARGEM.lateral + fatia * i + fatia / 2;
        // null e zero não desenham barra (nem toco): o rótulo mostra "—" ou "0".
        const h = d.valor ? Math.max(2, (d.valor / maximo) * util) : 0;
        return (
          <g key={d.rotulo} className="animate-crescer motion-reduce:animate-none" style={{ animationDelay: `${i * 60}ms` }}>
            {h > 0 && (
              <rect
                data-barra
                x={centro - largura / 2}
                y={base - h}
                width={largura}
                height={h}
                rx="3"
                fill={d.parcial ? `url(#${hachura})` : undefined}
                strokeWidth={d.parcial ? 1 : 0}
                className={d.parcial ? "stroke-primaria" : "fill-primaria"}
              />
            )}
            <text x={centro} y={base - h - 4} textAnchor="middle" fontSize="11" fontWeight="700" className="fill-texto tabular-nums">
              {formatarInteiro(d.valor)}
            </text>
            <text x={centro} y={base + 13} textAnchor="middle" fontSize="10.5" className="fill-suave">
              {d.rotulo}
            </text>
            {d.parcial && (
              <text x={centro} y={base + 25} textAnchor="middle" fontSize="9.5" fontWeight="600" className="fill-suave">
                parcial
              </text>
            )}
          </g>
        );
      })}
    </g>
  );
}

function Horizontais({ dados, maximo }: { dados: DadoGrafico[]; maximo: number }) {
  const util = LARGURA_VIEWBOX - LARGURA_ROTULO - LARGURA_VALOR;
  return (
    <g aria-hidden="true">
      {dados.map((d, i) => {
        const meio = i * ALTURA_LINHA + ALTURA_LINHA / 2;
        const w = d.valor ? Math.max(2, (d.valor / maximo) * util) : 0;
        return (
          <g key={d.rotulo} className="animate-crescer [--crescer-de:0_100%_0_0] motion-reduce:animate-none" style={{ animationDelay: `${Math.min(i, 8) * 40}ms` }}>
            <text x={0} y={meio + 4} fontSize="11" className="fill-texto">
              {d.rotulo.length > 18 ? `${d.rotulo.slice(0, 17)}…` : d.rotulo}
            </text>
            <rect x={LARGURA_ROTULO} y={meio - ESPESSURA_LINHA / 2} width={util} height={ESPESSURA_LINHA} rx="4.5" className="fill-realce" />
            {w > 0 && (
              <rect data-barra x={LARGURA_ROTULO} y={meio - ESPESSURA_LINHA / 2} width={w} height={ESPESSURA_LINHA} rx="4.5" className="fill-info" />
            )}
            <text x={LARGURA_VIEWBOX} y={meio + 4} textAnchor="end" fontSize="11" fontWeight="600" className="fill-texto tabular-nums">
              {formatarInteiro(d.valor)}
            </text>
          </g>
        );
      })}
    </g>
  );
}
