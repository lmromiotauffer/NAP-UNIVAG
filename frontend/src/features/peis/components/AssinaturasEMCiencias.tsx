import { useState } from "react";
import { Badge } from "../../../components/ui/Badge";
import { classeSecao } from "../../../components/ui/Card";
import { cn } from "../../../lib/cn";
import { formatarDataHora } from "../../../lib/format";
import type { VersaoPeiDetalhe } from "../../../lib/types";
import { AVISO_ASSINATURA, ROTULO_PAPEL } from "../rotulos";

export function AssinaturasEMCiencias({ versao }: { versao: VersaoPeiDetalhe }) {
  // Só a assinatura ou ciência registrada depois da montagem chega animada: na abertura a seção já vem com o surgir do <main>.
  const [assinaturasIniciais] = useState(versao.assinaturas.length);
  const [cienciasIniciais] = useState(versao.ciencias.length);
  return (
    <>
      <section aria-labelledby="titulo-assinaturas" className={classeSecao}>
        <h2 id="titulo-assinaturas" className="m-0 font-titulo text-base font-bold">
          Assinaturas
        </h2>
        {versao.assinaturas.length === 0 ? (
          <p className="m-0 text-denso text-suave">Nenhuma assinatura registrada ainda.</p>
        ) : (
          <ul className="m-0 flex list-none flex-col gap-2 p-0">
            {versao.assinaturas.map((a, i) => (
              <li key={`${a.papel}-${a.em}`} className={cn("flex flex-col gap-0.5 text-corpo", i >= assinaturasIniciais && "animate-surgir")}>
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <Badge variante="realizado">{ROTULO_PAPEL[a.papel]}</Badge>
                  <strong className="font-semibold">{a.nome}</strong>
                  <span className="text-suave">em {formatarDataHora(a.em)}</span>
                </div>
                <span className="text-xs text-suave">{AVISO_ASSINATURA}</span>
              </li>
            ))}
          </ul>
        )}
        {versao.hashConteudo && <p className="m-0 text-xs break-all text-suave">Hash do conteúdo: {versao.hashConteudo}</p>}
      </section>
      {(versao.estado === "Publicada" || versao.estado === "Arquivada" || versao.ciencias.length > 0) && (
        <section aria-labelledby="titulo-ciencias" className={classeSecao}>
          <h2 id="titulo-ciencias" className="m-0 font-titulo text-base font-bold">
            Ciência da coordenação
          </h2>
          {versao.ciencias.length === 0 ? (
            <p className="m-0 text-denso text-suave">Nenhuma ciência registrada ainda.</p>
          ) : (
            <ul className="m-0 flex list-none flex-col gap-1 p-0 text-corpo">
              {versao.ciencias.map((c, i) => (
                <li key={`${c.coordenador}-${c.em}`} className={cn(i >= cienciasIniciais && "animate-surgir")}>
                  {c.coordenador} · Ciência registrada em {formatarDataHora(c.em)}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </>
  );
}
