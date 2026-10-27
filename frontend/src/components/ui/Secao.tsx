import { Maximize2 } from "lucide-react";
import { useId, type ReactNode } from "react";
import { Card, classeSecao } from "./Card";

/** Seção de leitura: título, nota opcional e texto com quebras de linha preservadas (ficha do PEI, encaminhamento, assinatura). */
export function SecaoTexto({ titulo, nota, children }: { titulo: string; nota?: string; children: ReactNode }) {
  const id = useId();
  return (
    <section aria-labelledby={id} className={classeSecao}>
      <h2 id={id} className="m-0 font-titulo text-base font-bold">
        {titulo}
      </h2>
      {nota && <p className="m-0 text-rotulo text-suave">{nota}</p>}
      <p className="m-0 text-corpo whitespace-pre-line">{children}</p>
    </section>
  );
}

export const classeDetalhes = "flex h-7 cursor-pointer items-center gap-1 rounded-md border pointer-coarse:min-h-11 border-borda px-2.5 text-xs font-semibold text-primaria transition-colors duration-150 hover:bg-realce";

/** Cartão com título e, opcionalmente, nota e botão "Detalhes" no canto superior direito (painel e produção). */
export function CartaoSecao({ titulo, nota, aoDetalhar, rotuloDetalhar, className, children }: { titulo: string; nota?: string; aoDetalhar?: () => void; rotuloDetalhar?: string; className?: string; children: ReactNode }) {
  const id = useId();
  return (
    <Card aria-labelledby={id} className={className}>
      {/* Ação sempre no canto superior direito, como nos demais cartões: o título é que quebra linha. */}
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id={id} className="m-0 min-w-0 font-titulo text-destaque font-bold">
          {titulo}
        </h2>
        {(nota || aoDetalhar) && (
          <div className="flex flex-none items-center gap-2">
            {nota && <span className="text-xs text-suave">{nota}</span>}
            {aoDetalhar && (
              <button type="button" onClick={aoDetalhar} aria-label={rotuloDetalhar} className={classeDetalhes}>
                <Maximize2 aria-hidden="true" className="size-3" />
                Detalhes
              </button>
            )}
          </div>
        )}
      </div>
      {children}
    </Card>
  );
}
