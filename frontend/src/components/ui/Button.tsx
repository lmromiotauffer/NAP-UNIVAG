import { Loader2 } from "lucide-react";
import type { ButtonHTMLAttributes, Ref } from "react";
import { cn } from "../../lib/cn";

const BASE =
  "inline-flex max-w-full cursor-pointer items-center justify-center gap-2 rounded-md font-corpo font-medium transition-[color,background-color,border-color,opacity,transform] duration-150 active:scale-[0.97] motion-reduce:active:scale-100 disabled:active:scale-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primaria disabled:cursor-not-allowed disabled:opacity-60";

const variantes: Record<NonNullable<Base["variante"]>, string> = {
  primaria: "bg-primaria text-primaria-texto hover:opacity-90",
  secundaria: "border border-borda bg-superficie text-texto hover:bg-realce",
  fantasma: "text-texto hover:bg-realce",
  perigo: "bg-urgente text-primaria-texto hover:opacity-90",
};

// Em tela de toque (pointer: coarse) todo botão chega a 44 px, mesmo com tamanho reduzido via className.
const tamanhos: Record<NonNullable<ButtonProps["tamanho"]>, string> = {
  sm: "h-8 px-3 text-xs pointer-coarse:min-h-11",
  md: "h-10 px-4 text-sm pointer-coarse:min-h-11",
  icone: "size-10 pointer-coarse:min-h-11 pointer-coarse:min-w-11",
};

type Base = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "aria-label"> & {
  ref?: Ref<HTMLButtonElement>;
  variante?: "primaria" | "secundaria" | "fantasma" | "perigo";
  carregando?: boolean;
};

// Botão só com ícone exige nome acessível.
type ButtonProps =
  | (Base & { tamanho?: "sm" | "md"; "aria-label"?: string })
  | (Base & { tamanho: "icone"; "aria-label": string });

export function Button({ variante = "primaria", tamanho = "md", carregando, disabled, className, children, type = "button", ...resto }: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || carregando}
      aria-busy={carregando || undefined}
      className={cn(BASE, variantes[variante], tamanhos[tamanho], className)}
      {...resto}
    >
      {carregando && <Loader2 aria-hidden="true" className="animate-spin size-4" />}
      {children}
    </button>
  );
}
