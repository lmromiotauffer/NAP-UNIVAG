import { Search } from "lucide-react";
import { useEffect, useState } from "react";
import { cn } from "../../lib/cn";
import { classeFiltro, Input } from "./Input";

/**
 * Campo de busca com debounce (300 ms) gravado na URL. `maxLength` é o limite da API para o campo de busca.
 * Quem monta remonta o campo ao trocar de aba (`key`).
 */
export function BuscaUrl({ id, rotulo, valor, aoMudar, maxLength, className }: { id: string; rotulo: string; valor: string; aoMudar: (v: string | null) => void; maxLength: number; className?: string }) {
  const [texto, setTexto] = useState(valor);
  // URL mudou por fora (voltar do navegador, link, "limpar filtros"): o campo acompanha. Se a URL só reflete o texto
  // digitado (aparado pelo debounce), mantém o campo como está para não perder espaços nem letras novas.
  useEffect(() => setTexto((t) => (t.trim() === valor ? t : valor)), [valor]);
  useEffect(() => {
    if (texto.trim() === valor) return;
    const t = setTimeout(() => aoMudar(texto.trim() || null), 300);
    return () => clearTimeout(t);
  }, [texto, valor, aoMudar]);
  return (
    <div className={cn("relative", className)}>
      <label htmlFor={id} className="sr-only">
        {rotulo}
      </label>
      <Search aria-hidden="true" className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-suave" />
      <Input id={id} type="search" autoComplete="off" value={texto} onChange={(e) => setTexto(e.target.value)} placeholder={rotulo} maxLength={maxLength} className={cn(classeFiltro, "w-full pl-9")} />
    </div>
  );
}
