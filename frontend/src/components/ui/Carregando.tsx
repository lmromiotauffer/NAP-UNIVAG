import type { ReactNode } from "react";

/** Contêiner dos skeletons: `role="status"` anuncia o rótulo ao leitor de tela (aria-busy sozinho fica mudo). */
export function Carregando({ rotulo, className, children }: { rotulo: string; className?: string; children: ReactNode }) {
  return (
    <div role="status" className={className}>
      <span className="sr-only">{rotulo}</span>
      {children}
    </div>
  );
}
