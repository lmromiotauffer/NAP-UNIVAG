import { Link } from "react-router";
import { Button } from "../../../components/ui/Button";
import { Modal } from "../../../components/ui/Modal";
import { diaCivil, formatarDataCivil, formatarHora, segundaDoDia } from "../../../lib/format";
import type { FeriadoCriado } from "../services/administracaoService";

/** Compromissos e atendimentos que já existiam na data do novo feriado: nada é apagado, a resolução é na agenda. */
export function FeriadoConflitosDialog({ conflitos, aoFechar }: { conflitos: FeriadoCriado["conflitos"]; aoFechar: () => void }) {
  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo="Feriado cadastrado com conflitos"
      rodape={
        <Button variante="secundaria" onClick={aoFechar}>
          Fechar
        </Button>
      }
    >
      <p className="mb-3 text-sm">
        {conflitos.length === 1 ? "Há 1 item" : `Há ${conflitos.length} itens`} na agenda nesta data. Nada foi apagado. Resolva cada compromisso na agenda.
      </p>
      <ul className="m-0 flex max-h-72 list-none flex-col gap-2 overflow-y-auto p-0">
        {conflitos.map((c) => (
          <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-borda px-3 py-2 text-denso">
            <div className="min-w-0">
              <div className="font-semibold">{c.titulo === c.tipo ? c.profissional : c.titulo}</div>
              <div className="text-suave">
                {c.tipo} · {c.titulo === c.tipo ? "" : `${c.profissional} · `}{formatarDataCivil(diaCivil(c.inicio))}, {formatarHora(c.inicio)}–{formatarHora(c.fim)}
              </div>
            </div>
            <Link
              to={`/agenda?semana=${segundaDoDia(diaCivil(c.inicio))}&profissional=${c.profissionalId}`}
              onClick={aoFechar}
              aria-label={`Abrir a semana na agenda: ${c.titulo}`}
              className="text-rotulo font-semibold text-primaria"
            >
              Ver na agenda
            </Link>
          </li>
        ))}
      </ul>
    </Modal>
  );
}
