import type { ReactNode } from "react";
import { cn } from "../../lib/cn";

/** Par rótulo/valor de uma lista de definições (<dl>) nas telas de detalhe. */
export function Dado({ rotulo, children, classeValor }: { rotulo: string; children: ReactNode; classeValor?: string }) {
  return (
    <div>
      <dt className="text-xs text-suave">{rotulo}</dt>
      <dd className={cn("m-0", classeValor)}>{children}</dd>
    </div>
  );
}
