import { formatarHora, hojeCivil } from "../../../lib/format";
import type { EscalaSemana } from "../../../lib/types";
import { nomeDia } from "./GradeSemanal";

export function LegendaAgenda({ equipe, agora, escala }: { equipe: boolean; agora: Date; escala?: EscalaSemana }) {
  const semAtendimento = escala?.dias.filter((d) => d.semAtendimento && !d.feriado).map((d) => nomeDia(d.data)) ?? [];
  // A linha "agora" só existe na coluna de hoje: em outra semana a legenda não a anuncia.
  const comHoje = escala?.dias.some((d) => d.data === hojeCivil(agora)) ?? false;
  return (
    <div className="flex flex-wrap gap-x-4 gap-y-1 border-t border-borda px-4 py-3 text-xs text-suave">
      {equipe && (
        <span>
          Em “Toda a equipe” cada bloco mostra as iniciais do aluno ou da atividade; passe o mouse ou abra o bloco para ver o nome completo ou escolha um
          profissional para ver os nomes.
        </span>
      )}
      <span>Hachurado = ausência</span>
      <span>Tracejado = bloqueio</span>
      {comHoje && <span>Linha vermelha = agora ({formatarHora(agora.toISOString())})</span>}
      {semAtendimento.length > 0 && <span>{semAtendimento.join(", ")} sem atendimento na escala</span>}
    </div>
  );
}
