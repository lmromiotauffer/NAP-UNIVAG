import { CircleHelp, Ellipsis, KeyRound, LogOut } from "lucide-react";
import { Link, NavLink } from "react-router";
import { SeloNaoLidas } from "./SeloNaoLidas";
import { AlternarTema } from "./AlternarTema";
import type { PropsNavegacao } from "./TrilhoNavegacao";

// Rótulo truncado (texto grande, telas de 320 px): o nome completo segue no leitor de tela.
const rotuloBarra = "max-w-full truncate";
const botaoBarra = "flex min-h-12 min-w-0 flex-1 cursor-pointer flex-col items-center justify-center gap-[3px] text-micro font-semibold transition-[color,transform] duration-150 active:scale-95 motion-reduce:active:scale-100";
const itemMenu = "flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-3 py-2.5 text-left text-sm text-texto transition-colors duration-150 hover:bg-linha pointer-coarse:min-h-11";

export function BarraInferior({ itens, naoLidas, perfil, aoSair, aoAbrirAjuda }: PropsNavegacao) {
  const noMais = itens.filter((i) => i.noMais);
  return (
    <nav
      aria-label="Principal"
      className="fixed inset-x-0 bottom-0 z-10 flex justify-around border-t border-borda bg-superficie px-1 pt-1.5 pb-[calc(8px+env(safe-area-inset-bottom))] md:hidden"
    >
      {itens.filter((i) => !i.noMais).map(({ rota, rotulo, rotuloCurto, Icone, contador }) => (
        <NavLink key={rota} to={rota} end={rota === "/"} className={`${botaoBarra} text-suave aria-[current=page]:text-primaria`}>
          <span className="relative">
            <Icone aria-hidden="true" className="size-5" />
            {contador && <SeloNaoLidas total={naoLidas} />}
          </span>
          {rotuloCurto ? (
            <>
              <span aria-hidden="true" className={rotuloBarra}>{rotuloCurto}</span>
              {/* O nome acessível contém o texto visível (WCAG 2.5.3): quem diz "Atendimentos" por voz aciona "Atend.". */}
              <span className="sr-only">{rotulo.startsWith(rotuloCurto.replace(/\.$/, "")) ? rotulo : `${rotuloCurto} (${rotulo})`}</span>
            </>
          ) : (
            <span className={rotuloBarra}>{rotulo}</span>
          )}
        </NavLink>
      ))}
      {/* Popover nativo: o navegador cuida de aria-expanded, Esc e clique fora. */}
      <button type="button" popoverTarget="menu-mais" className={`${botaoBarra} text-suave`}>
        <Ellipsis aria-hidden="true" className="size-5" />
        <span className={rotuloBarra}>Mais</span>
      </button>
      {/* Na camada superior (fixed), logo acima da barra: 48px do botão + 8px de padding e 8px de folga. */}
      <div
        id="menu-mais"
        popover="auto"
        className="fixed top-auto right-2 bottom-[calc(64px+env(safe-area-inset-bottom))] left-auto m-0 w-60 flex-col gap-0.5 rounded-xl border border-borda bg-superficie p-1.5 text-texto shadow-lg open:flex"
      >
        <div className="flex items-center gap-2.5 border-b border-borda px-3 pt-1.5 pb-2.5">
          <span aria-hidden="true" className="grid size-8 flex-none place-items-center rounded-full bg-bloco text-xs font-bold text-bloco-texto">
            {perfil.sigla}
          </span>
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold">{perfil.nome}</div>
            <div className="truncate text-xs text-suave">{perfil.rotulo}</div>
          </div>
        </div>
        <button
          type="button"
          className={itemMenu}
          onClick={() => {
            document.getElementById("menu-mais")?.hidePopover();
            aoAbrirAjuda();
          }}
        >
          <CircleHelp aria-hidden="true" className="size-[18px] text-primaria" />
          Ajuda
        </button>
        {noMais.map(({ rota, rotulo, Icone }) => (
          <Link key={rota} to={rota} className={itemMenu} onClick={() => document.getElementById("menu-mais")?.hidePopover()}>
            <Icone aria-hidden="true" className="size-[18px] text-suave" />
            {rotulo}
          </Link>
        ))}
        <AlternarTema className={itemMenu} />
        <Link to="/definir-senha" className={itemMenu}>
          <KeyRound aria-hidden="true" className="size-[18px] text-suave" />
          Trocar senha
        </Link>
        <button type="button" className={itemMenu} onClick={aoSair}>
          <LogOut aria-hidden="true" className="size-[18px] text-suave" />
          Sair
        </button>
      </div>
    </nav>
  );
}
