import type { ReactNode } from "react";
import { cn } from "../../lib/cn";
import { classeIconeTitulo } from "./Card";

/** `nivel="h1"` quando o estado vazio é a página inteira (404, acesso negado); dentro de seções, o título fica em <p>. */
export function EmptyState({ titulo, descricao, acao, icone, nivel = "p" }: { titulo: string; descricao?: string; acao?: ReactNode; icone?: ReactNode; nivel?: "p" | "h1" | "h2" }) {
  const Titulo = nivel;
  return (
    // `flex-1 justify-center`: dentro de uma moldura que estica, o estado vazio fica no meio da altura sobrando.
    <div className="flex flex-1 flex-col items-center justify-center gap-2 px-4 py-10 text-center">
      {icone && (
        <span aria-hidden="true" className={cn(classeIconeTitulo, "mb-1 size-12 rounded-2xl [&>svg]:size-6")}>
          {icone}
        </span>
      )}
      <Titulo className="m-0 font-titulo text-base font-bold text-texto">{titulo}</Titulo>
      {descricao && <p className="max-w-md text-sm text-suave">{descricao}</p>}
      {acao}
    </div>
  );
}
