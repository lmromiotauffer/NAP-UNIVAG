import { formatarData, formatarIntervalo } from "../../../lib/format";
import type { ConflitoAgenda } from "../../../lib/types";

/** Itens que ocupam o horário de um bloqueio recusado (409 com `conflitos`). */
export function ListaConflitos({ conflitos }: { conflitos: ConflitoAgenda[] }) {
  if (conflitos.length === 0) return null;
  return (
    <ul aria-label="Itens em conflito" className="mt-2 list-disc pl-5">
      {conflitos.map((c) => (
        <li key={c.id}>
          {c.tipo} · {formatarData(c.inicio)} · {formatarIntervalo(c.inicio, c.fim)}
          {c.aluno ? ` · ${c.aluno}` : ""}
          {c.profissional ? ` · ${c.profissional}` : ""}
        </li>
      ))}
    </ul>
  );
}
