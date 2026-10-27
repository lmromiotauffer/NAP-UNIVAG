import type { TableHTMLAttributes } from "react";
import { cn } from "../../lib/cn";

// Classes das células das tabelas de listagem (o painel usa tabela própria, mais densa).
// sticky: nas listas roláveis o cabeçalho fica visível; o sombreado substitui a borda inferior, que some ao rolar com border-collapse.
export const th = "sticky top-0 z-10 bg-superficie px-4 py-2.5 text-xs font-semibold tracking-[0.04em] text-suave uppercase shadow-[inset_0_-1px_0_var(--color-borda)]";
export const td = "px-4 py-2 align-middle";
// Linha de listagem com ação ("Abrir ficha", "Abrir"): realce suave ao passar o mouse, com transição curta.
export const trClicavel = "relative border-b border-borda text-corpo transition-colors duration-150 hover:bg-realce";
export const soMedio = "hidden sm:table-cell";
export const soLargo = "hidden lg:table-cell";
// Ação "Abrir" da linha: o pseudo-elemento cobre a linha inteira (trClicavel é relative), então o cursor da linha é o do botão.
export const classeAbrirLinha = "inline-flex cursor-pointer items-center gap-1 rounded-sm text-xs font-semibold whitespace-nowrap text-primaria after:absolute after:inset-0 after:content-['']";
// Moldura das listagens: a tabela (e sua paginação) dentro de uma seção arredondada.
const classeMolduraTabela = "overflow-hidden rounded-secao border border-borda bg-superficie";
// Lista de página inteira: no desktop ocupa o resto da viewport e, se a tabela não couber, só ela rola (paginação sempre visível).
export const classeMolduraRolavel = `${classeMolduraTabela} md:flex md:min-h-0 md:flex-1 md:flex-col`;

export function Table({ caption, className, children, ...resto }: TableHTMLAttributes<HTMLTableElement> & { caption?: string }) {
  return (
    // `relative`: ancora os `sr-only` (absolutos) das células; sem ele escapam da rolagem e alargam a página.
    <div className="relative min-h-0 overflow-x-auto">
      <table className={cn("w-full border-collapse text-left text-sm transition-opacity duration-150", className)} {...resto}>
        {caption && <caption className="sr-only">{caption}</caption>}
        {children}
      </table>
    </div>
  );
}
