import { BellRing, ChevronRight, Maximize2 } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { Badge } from "../../../components/ui/Badge";
import { Card } from "../../../components/ui/Card";
import { Modal } from "../../../components/ui/Modal";
import { classeDetalhes } from "../../../components/ui/Secao";
import type { VarianteSemantica } from "../../../components/ui/variantes";
import { cn } from "../../../lib/cn";
import { formatarInteiro } from "../../../lib/format";
import type { Alerta } from "../../../lib/types";

const VISIVEIS = 5;
// Faltas = urgente; compromisso sem registro aguarda o profissional. O texto do Badge repete o significado.
const TIPOS: Record<string, { variante: VarianteSemantica; rotulo: string; cor: string }> = {
  faltas_recorrentes: { variante: "urgente", rotulo: "Urgente", cor: "text-urgente" },
  sem_desfecho: { variante: "aguardando", rotulo: "Aguardando", cor: "text-aguardando-texto" },
  "encaminhamento-sem-acolhimento": { variante: "urgente", rotulo: "Urgente", cor: "text-urgente" },
  "encaminhamento-sem-destinatario": { variante: "aguardando", rotulo: "Aguardando", cor: "text-aguardando-texto" },
  "pei-pendente": { variante: "aguardando", rotulo: "Aguardando", cor: "text-aguardando-texto" },
  "pei-sem-coordenador": { variante: "aguardando", rotulo: "Aguardando", cor: "text-aguardando-texto" },
};
const PADRAO = { variante: "neutro" as const, rotulo: "Pendente", cor: "text-texto" };
// Link de ação do alerta: mesmo hover e transição do botão "Detalhes".
const classeAcao = "ml-auto flex items-center gap-1 rounded-full bg-realce px-2.5 py-1.5 text-xs font-semibold whitespace-nowrap text-primaria transition-colors hover:bg-primaria/15 pointer-coarse:min-h-11";

/** Mais dias parados / ocorrências primeiro; sem número por último. */
const ordenarAlertas = (a: Alerta[]) => [...a].sort((x, y) => (y.diasOuOcorrencias ?? -1) - (x.diasOuOcorrencias ?? -1));

function Itens({ alertas }: { alertas: Alerta[] }) {
  return (
    <ol className="m-0 flex list-none flex-col p-0">
      {alertas.map((a, i) => {
        const tipo = TIPOS[a.tipo] ?? PADRAO;
        return (
          <li key={`${a.tipo}-${a.alunoId ?? i}`} className="flex flex-wrap items-center gap-x-2.5 gap-y-1.5 border-t border-borda py-2">
            <span className={cn("w-11 flex-none font-titulo text-[1.375rem] leading-none font-black tracking-[-0.03em] tabular-nums", tipo.cor)}>{formatarInteiro(a.diasOuOcorrencias)}</span>
            {/* basis-32 em rem: com texto grande o link desce para a linha de baixo em vez de espremer o detalhe. */}
            <div className="min-w-0 flex-1 basis-32 text-denso leading-[1.3]">
              <div className="flex flex-wrap items-center gap-x-1.5 gap-y-0.5">
                <Badge variante={tipo.variante}>{tipo.rotulo}</Badge>
                <span className="font-semibold break-words">{a.titulo}</span>
              </div>
              <div className="mt-0.5 text-xs break-words text-suave">{a.detalhe}</div>
            </div>
            {a.tipo.startsWith("encaminhamento-") ? (
              // O SIP não consulta alunos: o alerta leva à lista de encaminhamentos, que todos os perfis destinatários acessam.
              <Link to="/encaminhamentos" className={classeAcao}>
                Encaminhamentos
                <ChevronRight aria-hidden="true" className="size-3" />
              </Link>
            ) : a.alunoId ? (
              // O detalhe começa pelo nome do aluno: cada link tem nome acessível próprio.
              <Link to={`/alunos/${a.alunoId}`} aria-label={`Abrir ficha: ${a.detalhe}`} className={classeAcao}>
                Abrir ficha
                <ChevronRight aria-hidden="true" className="size-3" />
              </Link>
            ) : a.tipo === "sem_desfecho" ? (
              <Link to="/agenda" className={classeAcao}>
                Agenda
                <ChevronRight aria-hidden="true" className="size-3" />
              </Link>
            ) : (
              <span />
            )}
          </li>
        );
      })}
    </ol>
  );
}

export function BlocoPendencias({ alertas, className }: { alertas: Alerta[]; className?: string }) {
  const [todas, setTodas] = useState(false);
  const ordenados = ordenarAlertas(alertas);
  return (
    <Card aria-labelledby="titulo-pendencias" className={cn("gap-0", className)}>
      <div className="mb-1 flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="titulo-pendencias" className="m-0 flex items-center gap-2 font-titulo text-destaque font-bold">
          <BellRing aria-hidden="true" className="size-4 text-urgente" />
          Pendente
        </h2>
        <div className="flex items-center gap-2">
          <span className="text-xs text-suave">por dias parados</span>
          {ordenados.length > VISIVEIS && (
            <button type="button" onClick={() => setTodas(true)} className={classeDetalhes}>
              <Maximize2 aria-hidden="true" className="size-3" />
              Ver todas ({ordenados.length})
            </button>
          )}
        </div>
      </div>
      {ordenados.length === 0 ? <p className="m-0 py-4 text-sm text-suave">Nenhuma pendência. Faltas recorrentes e compromissos sem desfecho aparecem aqui.</p> : <Itens alertas={ordenados.slice(0, VISIVEIS)} />}
      <Modal aberto={todas} aoFechar={() => setTodas(false)} titulo={`Pendências (${ordenados.length})`}>
        <Itens alertas={ordenados} />
      </Modal>
    </Card>
  );
}
