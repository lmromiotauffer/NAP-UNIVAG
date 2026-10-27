import { ChevronLeft, ChevronRight } from "lucide-react";
import { useEffect } from "react";
import { Button } from "./Button";

export function Paginacao({ pagina, tamanho, total, aoMudar }: { pagina: number; tamanho: number; total: number; aoMudar: (pagina: number) => void }) {
  const porPagina = Math.max(1, tamanho);
  const inicio = total === 0 ? 0 : (pagina - 1) * porPagina + 1;
  const fim = Math.min(pagina * porPagina, total);
  const ultima = Math.max(1, Math.ceil(total / porPagina));
  // Página além do fim (URL editada ou último item excluído): a API devolve a lista vazia, então volta à última.
  // shortcut: onde aoMudar faz push na URL, "Voltar" reabre a página inválida e redireciona de novo; usar replace se incomodar.
  useEffect(() => {
    if (total > 0 && pagina > ultima) aoMudar(ultima);
  }, [pagina, ultima, total, aoMudar]);
  return (
    <nav aria-label="Paginação" className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm text-suave">
      <span aria-live="polite" className="tabular-nums">
        Mostrando {inicio}–{fim} de {total}
      </span>
      <div className="flex gap-2">
        <Button tamanho="sm" variante="secundaria" disabled={pagina <= 1} onClick={() => aoMudar(pagina - 1)}>
          <ChevronLeft aria-hidden="true" className="size-4" />
          Anterior
        </Button>
        <Button tamanho="sm" variante="secundaria" disabled={pagina >= ultima} onClick={() => aoMudar(pagina + 1)}>
          Próxima
          <ChevronRight aria-hidden="true" className="size-4" />
        </Button>
      </div>
    </nav>
  );
}
