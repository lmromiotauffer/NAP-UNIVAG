import { useState } from "react";
import { Alert } from "../../../components/ui/Alert";
import { ConfirmDialog } from "../../../components/ui/ConfirmDialog";
import { mensagemErro } from "../../../lib/api";
import { formatarDataCivil, formatarInteiro, somarDias } from "../../../lib/format";
import type { SemanaProducao } from "../../../lib/types";
import { useFecharSemana } from "../hooks/useProducao";

/** Confirmação de fechamento. Com pendências o aviso é obrigatório e a confirmação envia `confirmarPendencias`; um 422 do servidor aparece aqui. */
export function FecharSemanaDialog({ semana, aoConcluir, aoCancelar }: { semana: SemanaProducao; aoConcluir: () => void; aoCancelar: () => void }) {
  const [erro, setErro] = useState<string | null>(null);
  const fechar = useFecharSemana();
  const periodo = `${formatarDataCivil(semana.semana)} a ${formatarDataCivil(somarDias(semana.semana, 6))}`;
  const mensagem =
    semana.pendentes > 0
      ? `Esta semana tem ${formatarInteiro(semana.pendentes)} pendências de classificação. Fechar mesmo assim?`
      : `Fechar a semana de ${periodo} de ${semana.profissional}? Depois disso só a coordenação pode reabri-la.`;

  async function confirmar() {
    setErro(null);
    try {
      await fechar.mutateAsync({ profissionalId: semana.profissionalId, semana: semana.semana, confirmarPendencias: semana.pendentes > 0 });
      aoConcluir();
    } catch (e) {
      setErro(mensagemErro(e));
    }
  }

  return (
    <ConfirmDialog aberto titulo="Fechar semana" mensagem={mensagem} confirmar="Fechar semana" aoConfirmar={() => void confirmar()} aoCancelar={aoCancelar} carregando={fechar.isPending}>
      {erro && <Alert variante="urgente" className="animate-surgir">{erro}</Alert>}
    </ConfirmDialog>
  );
}
