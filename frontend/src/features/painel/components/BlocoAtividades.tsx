import { Clock, Presentation } from "lucide-react";
import { useId } from "react";
import { Link } from "react-router";
import { formatarData, formatarDuracao, formatarHora, formatarInteiro } from "../../../lib/format";
import type { Painel } from "../../../lib/types";
import { ROTULO_TIPO_ATIVIDADE, ROTULO_TURNO } from "../../atividades/rotulos";
import { CartaoIndicador } from "./CartaoIndicador";

/** Atividades nos cursos do mês corrente, no escopo do perfil (a API só envia o bloco a quem consulta atividades). */
export function BlocoAtividades({ dados, podeAbrir }: { dados: NonNullable<Painel["atividades"]>; podeAbrir: boolean }) {
  const id = useId();
  return (
    <section aria-labelledby={id} className="flex min-w-0 flex-col gap-2">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id={id} className="m-0 font-titulo text-destaque font-bold">
          Atividades nos cursos
        </h2>
        {podeAbrir && (
          <Link to="/atividades" className="inline-flex items-center rounded-sm text-xs font-semibold text-primaria underline-offset-2 hover:underline pointer-coarse:min-h-11">
            Ver atividades
          </Link>
        )}
      </div>
      <div className="grid grid-cols-2 gap-2 sm:gap-3">
        <CartaoIndicador titulo="Atividades no mês" valor={dados.noMes} formatar={formatarInteiro} base="registradas no mês corrente" Icone={Presentation} classeIcone="text-atividade" />
        <CartaoIndicador titulo="Horas no mês" valor={dados.minutosNoMes} formatar={formatarDuracao} base="soma da duração das atividades do mês" Icone={Clock} classeIcone="text-atividade" />
      </div>
      {dados.recentes.length > 0 && (
        <ul aria-label="Atividades recentes" className="m-0 flex list-none flex-col gap-1.5 rounded-cartao border border-borda bg-superficie p-3.5 text-denso">
          {dados.recentes.map((a) => (
            <li key={a.id} className="flex flex-wrap items-baseline justify-between gap-x-3">
              <span className="font-semibold">{a.curso ?? "Institucional"}</span>
              <span className="text-suave">
                {ROTULO_TIPO_ATIVIDADE[a.tipo]} · {formatarData(a.inicio)} · {ROTULO_TURNO[a.turno]} {formatarHora(a.inicio)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
