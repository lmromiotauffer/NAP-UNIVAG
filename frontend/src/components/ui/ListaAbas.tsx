import { useLayoutEffect, useRef, type KeyboardEvent } from "react";
import { cn } from "../../lib/cn";

/**
 * Lista de abas no padrão WAI-ARIA (ativação automática): setas/Home/End movem foco e seleção.
 * O painel fica com a página: `<div role="tabpanel" id={`painel-${chave}`} aria-labelledby={`aba-${chave}`}>`, sem tabIndex
 * quando o painel tem conteúdo focável; painel só de texto (ex.: resumo da ficha) leva `tabIndex={0}`.
 */
export function ListaAbas<T extends string>({
  abas,
  atual,
  aoTrocar,
  rotulo,
}: {
  abas: readonly { chave: T; rotulo: string }[];
  atual: T;
  aoTrocar: (chave: T) => void;
  rotulo: string;
}) {
  const refs = useRef<Partial<Record<T, HTMLButtonElement | null>>>({});
  const indicador = useRef<HTMLSpanElement>(null);
  // O sublinhado é um elemento só: ao trocar de aba ele desliza até a nova (como o destaque do trilho), em vez de reaparecer.
  useLayoutEffect(() => {
    const posicionar = () => {
      const ativa = refs.current[atual];
      const linha = indicador.current;
      if (!ativa || !linha) return;
      linha.style.width = `${ativa.offsetWidth}px`;
      linha.style.transform = `translateX(${ativa.offsetLeft}px)`;
      // No celular a lista rola na horizontal: a aba ativa (ex.: vinda da URL) fica à vista.
      ativa.scrollIntoView?.({ inline: "nearest", block: "nearest" });
    };
    posicionar();
    // A fonte própria chega depois do primeiro layout e muda a largura das abas: mede de novo quando ela carrega e ao redimensionar.
    void document.fonts?.ready.then(posicionar);
    window.addEventListener("resize", posicionar);
    return () => window.removeEventListener("resize", posicionar);
  }, [atual, abas]);

  function aoTeclar(e: KeyboardEvent<HTMLDivElement>) {
    const indice = abas.findIndex((a) => a.chave === atual);
    const destino = { ArrowRight: indice + 1, ArrowLeft: indice - 1, Home: 0, End: abas.length - 1 }[e.key];
    if (destino === undefined) return;
    e.preventDefault();
    const proxima = abas[(destino + abas.length) % abas.length];
    if (!proxima) return;
    aoTrocar(proxima.chave);
    refs.current[proxima.chave]?.focus();
  }

  return (
    // [scrollbar-width:none]: no celular a lista rola na horizontal, mas a barra ficava desenhada sob as abas (como no trilho, rola por toque, setas e scrollIntoView).
    <div role="tablist" aria-label={rotulo} onKeyDown={aoTeclar} className="relative flex shrink-0 gap-0.5 overflow-x-auto overflow-y-hidden border-b border-borda [scrollbar-width:none]">
      <span
        ref={indicador}
        aria-hidden="true"
        className="absolute bottom-0 left-0 h-0.5 w-0 bg-primaria transition-[transform,width] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] motion-reduce:transition-none"
      />
      {abas.map((a) => (
        <button
          key={a.chave}
          ref={(el) => {
            refs.current[a.chave] = el;
          }}
          type="button"
          role="tab"
          id={`aba-${a.chave}`}
          aria-selected={atual === a.chave}
          aria-controls={atual === a.chave ? `painel-${a.chave}` : undefined}
          tabIndex={atual === a.chave ? 0 : -1}
          onClick={() => aoTrocar(a.chave)}
          className={cn(
            "-mb-px cursor-pointer border-b-2 border-transparent px-3.5 py-2.5 text-denso font-semibold whitespace-nowrap transition-colors duration-150 pointer-coarse:min-h-11 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primaria",
            atual === a.chave ? "text-texto" : "text-suave hover:text-texto",
          )}
        >
          {a.rotulo}
        </button>
      ))}
    </div>
  );
}
