import type { ReactNode } from "react";
import { cn } from "../../../lib/cn";
import { formatarData, formatarDataCivil, formatarInteiro } from "../../../lib/format";
import type { AlunoFicha } from "../../../lib/types";

function Cartao({ rotulo, valor, nota, destaque, numero }: { rotulo: string; valor: ReactNode; nota?: string; destaque?: boolean; numero?: boolean }) {
  return (
    <div className={cn("rounded-cartao px-[18px] py-4", destaque ? "bg-bloco text-bloco-texto" : "border border-borda bg-superficie")}>
      <div className={cn("text-xs", destaque ? "text-bloco-texto/70" : "text-suave")}>{rotulo}</div>
      <div className={numero ? "mt-1.5 font-titulo text-[2.125rem] leading-none font-black tracking-[-0.04em] tabular-nums" : "mt-2 text-destaque font-semibold"}>
        {valor}
      </div>
      {nota && <div className={cn("mt-1 text-xs", destaque ? "text-bloco-texto/70" : "text-suave")}>{nota}</div>}
    </div>
  );
}

/** Cartões do topo da ficha. Coordenação de curso recebe `resumo` nulo: totais clínicos aparecem como "—". */
export function ResumoAcompanhamento({ aluno }: { aluno: AlunoFicha }) {
  const { resumo } = aluno;
  return (
    <div className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-3">
      <Cartao destaque numero rotulo="Atendimentos" valor={formatarInteiro(resumo?.totalAtendimentos)} nota="histórico completo" />
      <Cartao
        numero
        rotulo="Faltas"
        valor={<span className={resumo && resumo.totalFaltas > 0 ? "text-urgente" : undefined}>{formatarInteiro(resumo?.totalFaltas)}</span>}
      />
      <Cartao rotulo="Profissional responsável" valor={aluno.profissional || "—"} />
      <Cartao
        rotulo="Acompanhamento desde"
        valor={formatarDataCivil(aluno.inicioAcompanhamento)}
        nota={resumo ? `último atendimento ${formatarData(resumo.ultimoAtendimento)}` : undefined}
      />
    </div>
  );
}
