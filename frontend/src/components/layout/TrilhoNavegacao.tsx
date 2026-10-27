import { CircleHelp, KeyRound, LogOut } from "lucide-react";
import { useLayoutEffect, useRef } from "react";
import { Link, NavLink, useLocation } from "react-router";
import type { ItemNav } from "./AppShell";
import { LogoUnivag } from "../ui/LogoUnivag";
import { SeloNaoLidas } from "./SeloNaoLidas";
import { AlternarTema } from "./AlternarTema";

export type PropsNavegacao = {
  itens: ItemNav[];
  naoLidas: number;
  perfil: { nome: string; sigla: string; rotulo: string };
  aoSair: () => void;
  aoAbrirAjuda: () => void;
};

// Toque: encolhe 150 ms e volta; em prefers-reduced-motion fica só a troca de cor.
const botaoTrilho = "flex w-[100px] flex-none cursor-pointer flex-col items-center gap-[3px] rounded-campo py-1.5 text-mini leading-[1.1] font-semibold transition-[color,background-color,transform] duration-150 active:scale-95 motion-reduce:active:scale-100";

export function TrilhoNavegacao({ itens, naoLidas, perfil, aoSair, aoAbrirAjuda }: PropsNavegacao) {
  const trilho = useRef<HTMLElement>(null);
  const destaque = useRef<HTMLSpanElement>(null);
  const { pathname } = useLocation();
  // O destaque do item ativo é um elemento só, atrás dos itens: na troca de página ele desliza até o novo item em vez de reaparecer.
  useLayoutEffect(() => {
    const posicionar = () => {
      const aside = trilho.current;
      const ativo = aside?.querySelector<HTMLElement>('a[aria-current="page"]');
      const fundo = destaque.current;
      if (!aside || !fundo) return;
      // Sem item visível de onde deslizar (montagem, trilho recém-exibido, rota sem item antes): aparece no lugar, em vez de viajar desde o canto.
      const semOrigem = fundo.style.opacity !== "1";
      if (semOrigem) fundo.style.transition = "none";
      fundo.style.opacity = ativo ? "1" : "0";
      if (ativo) {
        // Medida em subpixel (offsetTop arredonda): o fundo fica exatamente sob o item, mesmo com o trilho rolado.
        const caixa = aside.getBoundingClientRect();
        const item = ativo.getBoundingClientRect();
        fundo.style.height = `${item.height}px`;
        fundo.style.transform = `translate(${item.left - caixa.left}px, ${item.top - caixa.top + aside.scrollTop}px)`;
      }
      if (semOrigem) {
        void fundo.offsetHeight; // fixa a posição antes de devolver a transição
        fundo.style.transition = "";
      }
    };
    posicionar();
    // Trilho oculto no celular mede zero: ao cruzar o breakpoint, mede de novo.
    window.addEventListener("resize", posicionar);
    return () => window.removeEventListener("resize", posicionar);
  }, [pathname]);
  return (
    <aside ref={trilho} className="sticky top-0 hidden h-dvh w-28 flex-none flex-col items-center gap-0.5 overflow-y-auto border-r border-borda bg-superficie py-3 [scrollbar-width:none] md:flex">
      <span
        ref={destaque}
        aria-hidden="true"
        className="absolute top-0 left-0 -z-10 w-[100px] rounded-campo bg-realce opacity-0 transition-[transform,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] motion-reduce:transition-none"
      />
      <LogoUnivag className="my-2 w-16" />
      <nav aria-label="Principal" className="contents">
        {itens.map(({ rota, rotulo, dica, Icone, contador }) => (
          <NavLink
            key={rota}
            to={rota}
            end={rota === "/"}
            title={dica}
            className={`${botaoTrilho} text-suave not-aria-[current=page]:hover:bg-linha aria-[current=page]:text-primaria`}
          >
            <span className="relative">
              <Icone aria-hidden="true" className="size-5" />
              {contador && <SeloNaoLidas total={naoLidas} />}
            </span>
            {rotulo}
          </NavLink>
        ))}
      </nav>
      <div className="mt-auto flex flex-none flex-col items-center gap-1 pt-2">
        <button type="button" onClick={aoAbrirAjuda} title="Ajuda: o que é cada área e o que as cores significam" className={`${botaoTrilho} border border-borda text-primaria hover:bg-linha`}>
          <CircleHelp aria-hidden="true" className="size-5" />
          Ajuda
        </button>
        <AlternarTema className={`${botaoTrilho} border border-borda text-suave hover:bg-linha`} />
      </div>
      <div
        role="img"
        aria-label={`${perfil.nome} · ${perfil.rotulo}`}
        title={`${perfil.nome} · ${perfil.rotulo}`}
        className="mt-1.5 grid size-8 flex-none place-items-center rounded-full bg-bloco text-xs font-bold text-bloco-texto"
      >
        {perfil.sigla}
      </div>
      <Link to="/definir-senha" title="Trocar a própria senha" className="mt-1 flex w-[100px] items-center justify-center gap-[5px] rounded-lg py-1.5 text-mini font-semibold text-suave transition-colors duration-150 hover:bg-linha pointer-coarse:min-h-11">
        <KeyRound aria-hidden="true" className="size-3.5" />
        Trocar senha
      </Link>
      <button
        type="button"
        onClick={aoSair}
        title="Sair do sistema"
        className="mt-1 flex w-[100px] cursor-pointer items-center justify-center gap-[5px] rounded-lg py-1.5 text-mini font-semibold text-suave transition-colors duration-150 hover:bg-linha pointer-coarse:min-h-11"
      >
        <LogOut aria-hidden="true" className="size-3.5" />
        Sair
      </button>
    </aside>
  );
}
