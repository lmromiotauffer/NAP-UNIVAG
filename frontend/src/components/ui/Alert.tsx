import type { HTMLAttributes } from "react";
import { cn } from "../../lib/cn";
import { classesSemanticas, type VarianteSemantica } from "./variantes";

export function Alert({ variante = "info", className, ...resto }: HTMLAttributes<HTMLDivElement> & { variante?: VarianteSemantica }) {
  return <div role={variante === "urgente" ? "alert" : undefined} className={cn("rounded-md px-3 py-2 text-sm", classesSemanticas[variante], className)} {...resto} />;
}
