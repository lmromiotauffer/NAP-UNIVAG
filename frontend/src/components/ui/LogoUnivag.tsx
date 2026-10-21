import { useSyncExternalStore } from "react";
import { cn } from "../../lib/cn";
import { assinarTema, temaAplicado } from "../layout/preferencias";

/** Logo sem pílula branca: colorido no tema claro, branco no escuro. */
export function LogoUnivag({ className }: { className?: string }) {
  const escuro = useSyncExternalStore(assinarTema, temaAplicado) === "escuro";
  return <img src={escuro ? "/logo-univag-branco.png" : "/logo-univag.png"} alt="UNIVAG" width={96} height={40} className={cn("h-auto w-24", className)} />;
}
