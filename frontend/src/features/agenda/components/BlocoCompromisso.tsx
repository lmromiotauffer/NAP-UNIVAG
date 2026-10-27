import { Bell, Repeat } from "lucide-react";
import type { CSSProperties } from "react";
import { cn } from "../../../lib/cn";
import type { EstiloCompromisso } from "./estiloCompromisso";

const CORES: Record<EstiloCompromisso["variante"], string> = {
  info: "bg-info-fundo text-info border-info",
  realizado: "bg-realizado-fundo text-realizado border-realizado",
  urgente: "bg-urgente-fundo text-urgente border-urgente",
  atividade: "bg-atividade-fundo text-atividade border-atividade",
  neutro: "bg-hachura text-texto border-suave",
};

const PADROES: Record<EstiloCompromisso["padrao"], string> = {
  solido: "border-l-[3px]",
  // Listras na cor do próprio texto (token semântico), legíveis nos dois temas.
  hachurado:
    "border bg-superficie [background-image:repeating-linear-gradient(135deg,color-mix(in_srgb,currentColor_18%,transparent)_0_6px,transparent_6px_12px)]",
  tracejado: "border border-dashed bg-superficie text-suave",
};

export function BlocoCompromisso({
  rotulo,
  texto,
  detalhe,
  estilo,
  dica,
  riscado,
  lembrete,
  remarcado,
  posicao,
  aoAbrir,
}: {
  /** Nome acessível completo: horário, tipo, pessoa e estado. */
  rotulo: string;
  texto: string;
  detalhe?: string;
  estilo: EstiloCompromisso;
  dica?: string;
  riscado?: boolean;
  lembrete?: boolean;
  remarcado?: boolean;
  posicao: CSSProperties;
  aoAbrir: () => void;
}) {
  return (
    <button
      type="button"
      aria-label={rotulo}
      title={dica}
      onClick={aoAbrir}
      style={posicao}
      className={cn(
        "absolute cursor-pointer overflow-hidden rounded-md px-1 py-1 text-left text-xs leading-tight ring-1 ring-superficie transition-[filter] duration-150 hover:brightness-95 focus-visible:z-20 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-primaria",
        CORES[estilo.variante],
        PADROES[estilo.padrao],
        lembrete && "z-10 min-h-6 rounded-full py-0.5",
      )}
    >
      <span aria-hidden="true" className={cn("flex items-center gap-1 font-semibold whitespace-nowrap", riscado && "line-through")}>
        {lembrete && <Bell className="size-3 flex-none" />}
        {remarcado && <Repeat className="size-3 flex-none" />}
        <span className="truncate">{texto}</span>
      </span>
      {detalhe && (
        <span aria-hidden="true" className="block truncate">
          {detalhe}
        </span>
      )}
    </button>
  );
}
