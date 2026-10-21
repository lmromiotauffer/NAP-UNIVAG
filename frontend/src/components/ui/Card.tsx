import type { HTMLAttributes } from "react";
import { cn } from "../../lib/cn";

// Seção de página (ficha, detalhes, assinatura); o `Card` é o bloco compacto do painel e da produção.
export const classeSecao = "flex flex-col gap-3 rounded-secao border border-borda bg-superficie px-[22px] py-5";

// Título (h1) de toda página; ícone ou selo ao lado entram com `cn(classeTituloPagina, "flex items-center gap-3")`.
// wrap-anywhere: com texto grande, uma palavra longa ("Administração") quebra em vez de alargar a página.
export const classeTituloPagina = "m-0 min-w-0 font-titulo text-titulo-pagina leading-[1.05] font-black tracking-[-0.02em] wrap-anywhere";

export function Card({ className, ...resto }: HTMLAttributes<HTMLElement>) {
  return <section className={cn("flex min-w-0 flex-col gap-2 rounded-cartao border border-borda bg-superficie px-[18px] py-3.5", className)} {...resto} />;
}

// Ícone quadrado ao lado do título da página.
export const classeIconeTitulo = "grid size-10 flex-none place-items-center rounded-xl bg-realce text-primaria";
