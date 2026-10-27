import { Moon, Sun } from "lucide-react";
import { useSyncExternalStore } from "react";
import { aplicarTema, assinarTema, salvarPreferencia, temaAplicado, type Tema } from "./preferencias";

export function AlternarTema({ className }: { className?: string }) {
  const tema = useSyncExternalStore(assinarTema, temaAplicado);
  const escuro = tema === "escuro";

  function alternar() {
    const novo: Tema = escuro ? "claro" : "escuro";
    const trocar = () => {
      aplicarTema(novo);
      salvarPreferencia("nap.tema", novo);
    };
    // A tela inteira cruza de um tema ao outro (View Transitions, ~250 ms) em vez de piscar; sem suporte ou com
    // prefers-reduced-motion, troca na hora.
    if (typeof document.startViewTransition === "function" && !matchMedia("(prefers-reduced-motion: reduce)").matches) document.startViewTransition(trocar);
    else trocar();
  }

  const Icone = escuro ? Sun : Moon;
  return (
    <button
      type="button"
      onClick={alternar}
      aria-label={escuro ? "Usar tema claro" : "Usar tema escuro"}
      title="Alternar entre tema claro e escuro"
      className={className}
    >
      <Icone aria-hidden="true" className="size-[18px]" />
      {escuro ? "Claro" : "Escuro"}
    </button>
  );
}
