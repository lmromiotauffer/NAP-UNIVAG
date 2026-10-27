import type { HTMLAttributes } from "react";
import { cn } from "../../lib/cn";
import { classesSemanticas, type VarianteSemantica } from "./variantes";

// Sempre com texto: a cor não é o único sinal.
export function Badge({ variante = "neutro", className, ...resto }: HTMLAttributes<HTMLSpanElement> & { variante?: VarianteSemantica }) {
  // transition-colors: a situação que muda depois de uma ação (ex.: "ativo" → "inativo") cruza de cor em vez de piscar.
  return <span className={cn("inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium transition-colors duration-150", classesSemanticas[variante], className)} {...resto} />;
}
