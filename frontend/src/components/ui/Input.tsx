import type { InputHTMLAttributes, Ref } from "react";
import { cn } from "../../lib/cn";

export const classeCampo =
  "w-full rounded-md border bg-superficie px-3 py-2 text-sm transition-[border-color] pointer-coarse:min-h-11 text-texto placeholder:text-suave focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-primaria disabled:opacity-60";

export function bordaCampo(invalid?: boolean): string {
  return invalid ? "border-urgente" : "border-borda-campo";
}

export function Input({ invalid, className, ref, ...resto }: InputHTMLAttributes<HTMLInputElement> & { invalid?: boolean; ref?: Ref<HTMLInputElement> }) {
  return <input ref={ref} aria-invalid={invalid || undefined} className={cn(classeCampo, bordaCampo(invalid), className)} {...resto} />;
}

// Barras de filtro das listagens. No celular os filtros dividem a linha por igual (11rem cabe "Especialidade: todas"; abaixo disso, um por linha);
// a partir de `sm` voltam ao tamanho do conteúdo.
export const classeFiltro = "min-h-[2.375rem] w-auto min-w-[9.5rem] flex-1 rounded-campo py-0 sm:min-w-[140px] sm:flex-none";
// Largura mínima de "dd/mm/aaaa" + ícone: no celular o par de datas quebra de linha em vez de cortar a data.
export const classeData = "min-h-[2.375rem] w-auto min-w-[9.5rem] flex-1 rounded-campo py-0 sm:flex-none";
