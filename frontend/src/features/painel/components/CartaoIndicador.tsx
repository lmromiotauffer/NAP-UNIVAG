import type { LucideIcon } from "lucide-react";
import { Maximize2 } from "lucide-react";
import { cn } from "../../../lib/cn";
import { classeDetalhes } from "../../../components/ui/Secao";

/** KPI do painel. `null` mostra "—" e o motivo ao lado; a base da conta só aparece quando há valor. */
export function CartaoIndicador({
  titulo,
  valor,
  formatar,
  base,
  motivoNaoCalculado,
  Icone,
  classeIcone,
  aoVerDetalhes,
  className,
}: {
  titulo: string;
  valor: number | null;
  formatar: (v: number | null) => string;
  base?: string;
  motivoNaoCalculado?: string | null;
  Icone?: LucideIcon;
  /** Classe de token do ícone (ex.: "text-urgente"). */
  classeIcone?: string;
  aoVerDetalhes?: () => void;
  className?: string;
}) {
  // Compacto: título no alto; valor e base (ou motivo) na mesma linha. O botão "Detalhes" é o mesmo dos demais cartões e,
  // como no CartaoRealizados, no celular o conteúdo desce abaixo dele; a partir do sm fica absoluto no canto sem mudar a
  // altura do cartão, e só o título reserva largura à direita (o valor também, quando o botão cresce para o toque).
  return (
    <div className={cn("relative flex min-w-0 flex-col gap-1.5 rounded-cartao border border-borda bg-superficie px-3.5 py-2.5 text-left text-texto sm:px-4", aoVerDetalhes && "pt-12 sm:pt-2.5 pointer-coarse:pt-16 pointer-coarse:sm:pt-2.5", className)}>
      {aoVerDetalhes && (
        <button type="button" onClick={aoVerDetalhes} aria-label={`Detalhes: ${titulo}`} className={cn(classeDetalhes, "absolute top-2.5 right-3")}>
          <Maximize2 aria-hidden="true" className="size-3" />
          Detalhes
        </button>
      )}
      <span className={cn("flex items-start gap-1.5 text-xs text-suave", aoVerDetalhes && "sm:pr-24")}>
        {Icone && <Icone aria-hidden="true" className={cn("mt-px size-3.5 flex-none", classeIcone)} />}
        {titulo}
      </span>
      <span className={cn("flex flex-wrap items-baseline gap-x-2 gap-y-0.5", aoVerDetalhes && "pointer-coarse:sm:pr-24")}>
        <span className="font-titulo text-[1.75rem] leading-none font-black tracking-[-0.04em] tabular-nums">{formatar(valor)}</span>
        {valor == null ? motivoNaoCalculado && <span className="text-xs text-suave">{motivoNaoCalculado}</span> : base && <span className="text-xs text-suave">{base}</span>}
      </span>
    </div>
  );
}
