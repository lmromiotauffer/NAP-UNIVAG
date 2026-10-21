import type { HTMLAttributes } from "react";
import { cn } from "../../lib/cn";
import { classesSemanticas, type VarianteSemantica } from "./variantes";

// Sempre com texto: a cor não é o único sinal.
export function Badge({ variante = "neutro", className, ...resto }: HTMLAttributes<HTMLSpanElement> & { variante?: VarianteSemantica }) {
  return <span className={cn("inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium", classesSemanticas[variante], className)} {...resto} />;
}
