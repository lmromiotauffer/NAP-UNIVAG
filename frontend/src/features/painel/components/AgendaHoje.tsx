import { Link } from "react-router";
import { Badge } from "../../../components/ui/Badge";
import { Card } from "../../../components/ui/Card";
import { cn } from "../../../lib/cn";
import { formatarIntervalo } from "../../../lib/format";
import type { Compromisso } from "../../../lib/types";
import { estiloCompromisso } from "../../agenda/components/estiloCompromisso";

/** Compromissos de hoje (da equipe ou do profissional, conforme o escopo que a API aplicou). */
export function AgendaHoje({ titulo, compromissos, mostrarProfissional, agora, className }: { titulo: string; compromissos: Compromisso[]; mostrarProfissional: boolean; agora: Date; className?: string }) {
  const ordenados = [...compromissos].sort((a, b) => a.inicio.localeCompare(b.inicio));
  return (
    <Card aria-labelledby="titulo-hoje" className={className}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="titulo-hoje" className="m-0 font-titulo text-destaque font-bold">
          {titulo}
        </h2>
        <Link to="/agenda" className="inline-flex items-center rounded-sm text-xs font-semibold text-primaria underline-offset-2 hover:underline pointer-coarse:min-h-11">
          Ver agenda
        </Link>
      </div>
      {ordenados.length === 0 ? (
        <p className="m-0 py-4 text-sm text-suave">Nenhum compromisso hoje.</p>
      ) : (
        <ul className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-2 p-0">
          {ordenados.map((c) => {
            const estilo = estiloCompromisso(c, agora);
            const emCurso = Date.parse(c.inicio) <= agora.getTime() && agora.getTime() < Date.parse(c.fim) && c.estado === "Agendado";
            return (
              <li key={c.id} className={cn("flex min-w-0 flex-col gap-1 rounded-xl bg-linha px-2.5 py-2", emCurso && "ring-2 ring-primaria")}>
                <div className="flex items-center justify-between gap-2 text-xs text-suave tabular-nums">
                  <span>
                    {formatarIntervalo(c.inicio, c.fim)}
                  </span>
                  {emCurso ? <Badge variante="info">Agora</Badge> : <Badge variante={estilo.variante}>{estilo.rotuloEstado}</Badge>}
                </div>
                <div className="truncate text-denso font-semibold">{c.aluno ?? c.titulo}</div>
                {mostrarProfissional && <div className="truncate text-xs text-suave">{c.profissional}</div>}
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
}
