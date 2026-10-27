import { Link } from "react-router";
import { Hourglass, Inbox, Stethoscope, TimerOff, type LucideIcon } from "lucide-react";
import { useId } from "react";
import { formatarInteiro } from "../../../lib/format";
import type { Painel } from "../../../lib/types";
import { CartaoIndicador } from "./CartaoIndicador";

type Cartao = { titulo: string; valor: number; base: string; Icone: LucideIcon; classeIcone: string };

/** Encaminhamentos abertos no escopo do perfil (SIP: os destinados a ele; coordenação de curso: os do curso). */
export function BlocoEncaminhamentos({ dados, ehSip, podeAbrir }: { dados: NonNullable<Painel["encaminhamentos"]>; ehSip: boolean; podeAbrir: boolean }) {
  const id = useId();
  const cartoes: Cartao[] = [
    { titulo: ehSip ? "Recebidos (abertos)" : "Abertos", valor: dados.abertos, base: "encaminhamentos sem alta nem cancelamento", Icone: Inbox, classeIcone: "text-info" },
    {
      titulo: "Aguardando acolhimento",
      valor: dados.aguardandoAcolhimento,
      base: dados.semAcolhimentoNoPrazo > 0 ? `${formatarInteiro(dados.semAcolhimentoNoPrazo)} fora do prazo de acolhimento` : "todos dentro do prazo de acolhimento",
      Icone: Hourglass,
      classeIcone: "text-aguardando-texto",
    },
    { titulo: "Em atendimento", valor: dados.emAtendimento, base: "acolhidos com atendimento em curso", Icone: Stethoscope, classeIcone: "text-realizado" },
    {
      titulo: "Sem atualização recente",
      valor: dados.semAtualizacao,
      base: `sem retorno há mais de ${dados.diasSemAtualizacao} dias`,
      Icone: TimerOff,
      classeIcone: "text-urgente",
    },
  ];
  return (
    <section aria-labelledby={id} className="flex min-w-0 flex-col gap-2">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id={id} className="m-0 font-titulo text-destaque font-bold">
          Encaminhamentos
        </h2>
        {podeAbrir && (
          <Link to="/encaminhamentos" className="inline-flex items-center rounded-sm text-xs font-semibold text-primaria underline-offset-2 hover:underline pointer-coarse:min-h-11">
            Ver encaminhamentos
          </Link>
        )}
      </div>
      <div className="grid grid-cols-2 gap-2 sm:gap-3 lg:grid-cols-4">
        {cartoes.map((c) => (
          <CartaoIndicador key={c.titulo} titulo={c.titulo} valor={c.valor} formatar={formatarInteiro} base={c.base} Icone={c.Icone} classeIcone={c.classeIcone} />
        ))}
      </div>
    </section>
  );
}
