import { useState } from "react";
import { Select } from "../../../components/ui/Select";
import { cn } from "../../../lib/cn";
import { diaDaSemana, formatarInteiro, formatarMesCurto } from "../../../lib/format";
import type { Producao } from "../../../lib/types";
import { minutosComSinal } from "./TabelaCategorias";

// Semana: um ponto por dia ("Seg"); mês: semanas recortadas ("1–6"); semestre e ano: meses ("set").
const rotuloPonto = (tipo: Producao["periodo"]["tipo"], inicio: string, fim: string) =>
  tipo === "Semana" ? diaDaSemana(inicio).curto : tipo === "Mes" ? `${Number(inicio.slice(8))}–${Number(fim.slice(8))}` : formatarMesCurto(Number(inicio.slice(5, 7)));

/** Barras em HTML/CSS: a altura é o tempo; cada barra tem nome acessível com ocorrências e tempo (parcial avisado). */
export function GraficoSerie({ producao, idTitulo }: { producao: Producao; idTitulo?: string }) {
  const [chave, setChave] = useState(producao.categorias[0]?.chave);
  const categoria = producao.categorias.find((c) => c.chave === chave) ?? producao.categorias[0];
  if (!categoria) return null;
  const pontos = producao.serie.map((s) => {
    const v = s.categorias.find((c) => c.chave === categoria.chave)?.total ?? { ocorrencias: null, minutos: 0 };
    const rotulo = rotuloPonto(producao.periodo.tipo, s.inicio, s.fim);
    const valores = [v.ocorrencias == null ? null : `${formatarInteiro(v.ocorrencias)} ocorrências`, minutosComSinal(v.minutos)].filter(Boolean).join(", ");
    return { chave: s.inicio, rotulo, minutos: v.minutos, parcial: s.parcial, nome: `${rotulo}${s.parcial ? " (parcial)" : ""}: ${valores}` };
  });
  const maximo = Math.max(1, ...pontos.map((p) => p.minutos));
  return (
    <div className="flex flex-col gap-2">
      {/* Título e seletor na mesma linha; em tela estreita o seletor desce inteiro. */}
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
        <h2 id={idTitulo} className="m-0 font-titulo text-destaque font-bold">
          Evolução no período
        </h2>
        <label className="flex min-w-0 flex-1 basis-60 items-center justify-end gap-2 text-xs text-suave">
          Categoria do gráfico
          <Select value={categoria.chave} onChange={(e) => setChave(e.target.value as typeof chave)} className="h-9 min-w-0 flex-1 py-0 text-rotulo sm:max-w-72">
            {producao.categorias.map((c) => (
              <option key={c.chave} value={c.chave}>
                {c.rotulo}
              </option>
            ))}
          </Select>
        </label>
      </div>
      {/* Cada barra tem largura mínima para o valor ("22h00") não sobrepor o vizinho: com 12 meses no celular o gráfico rola na horizontal. */}
      <ul aria-label={`Tempo por período: ${categoria.rotulo}`} className="m-0 flex h-24 list-none items-end gap-1.5 overflow-x-auto p-0">
        {pontos.map((p, i) => (
          <li key={p.chave} role="img" aria-label={p.nome} className="flex h-full min-w-9 flex-1 flex-col items-center justify-end gap-1">
            <span aria-hidden="true" className="text-mini font-bold tabular-nums">{minutosComSinal(p.minutos)}</span>
            {/* Cresce da base só na montagem (chave = início do ponto): trocar a categoria do seletor apenas troca a altura. */}
            <div
              aria-hidden="true"
              style={{ height: `${p.minutos > 0 ? Math.max(2, (p.minutos / maximo) * 100) : 0}%`, animationDelay: `${Math.min(i, 4) * 60}ms` }}
              className={cn("animate-crescer w-full max-w-10 rounded-t-md border border-primaria motion-reduce:animate-none", p.parcial ? "border-dashed bg-realce" : "bg-primaria")}
            />
            <span aria-hidden="true" className="text-mini whitespace-nowrap text-suave">{p.rotulo}{p.parcial && "*"}</span>
          </li>
        ))}
      </ul>
      {pontos.some((p) => p.parcial) && <p className="m-0 text-xs text-suave">* período ainda em andamento.</p>}
    </div>
  );
}
