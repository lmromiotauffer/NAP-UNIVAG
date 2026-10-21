import { Moon, Sun } from "lucide-react";
import { useSyncExternalStore } from "react";
import { cn } from "../../lib/cn";
import { aplicarTema, assinarTema, salvarPreferencia, temaAplicado, type Tema } from "./preferencias";

export function AlternarTema({ className }: { className?: string }) {
  const tema = useSyncExternalStore(assinarTema, temaAplicado);
  const escuro = tema === "escuro";

  function alternar() {
    const novo: Tema = escuro ? "claro" : "escuro";
    aplicarTema(novo);
    salvarPreferencia("nap.tema", novo);
  }

  const Icone = escuro ? Sun : Moon;
  return (
    <button
      type="button"
      onClick={alternar}
      aria-label={escuro ? "Usar tema claro" : "Usar tema escuro"}
      title="Alternar entre tema claro e escuro"
      className={cn(className)}
    >
      <Icone aria-hidden="true" className="size-[18px]" />
      {escuro ? "Claro" : "Escuro"}
    </button>
  );
}
