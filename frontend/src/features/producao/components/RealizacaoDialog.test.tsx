// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Compromisso } from "../../../lib/types";
import { producaoService } from "../services/producaoService";
import { RealizacaoDialog } from "./RealizacaoDialog";

vi.mock("../services/producaoService", () => ({ producaoService: { registrarRealizacao: vi.fn(), corrigirRealizacao: vi.fn() } }));
const registrar = vi.mocked(producaoService.registrarRealizacao);
const corrigir = vi.mocked(producaoService.corrigirRealizacao);

const compromisso: Compromisso = {
  id: "c1",
  tipo: "ReuniaoEquipe",
  titulo: "Reunião semanal",
  inicio: "2026-09-29T08:00:00-04:00",
  fim: "2026-09-29T09:00:00-04:00",
  profissionalId: "p1",
  profissional: "Ana Souza",
  alunoId: null,
  aluno: null,
  estado: "Agendado",
  estadoAtendimento: null,
  remarcadoDeId: null,
  descricao: null,
  versao: "v1",
  grupoId: null,
  participantes: null,
  podeGerirGrupo: false,
};

function montar(aoConcluir = vi.fn(), correcao = false) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <RealizacaoDialog compromisso={compromisso} correcao={correcao} aoConcluir={aoConcluir} aoCancelar={vi.fn()} />
    </QueryClientProvider>,
  );
  return { user: userEvent.setup(), aoConcluir };
}

beforeEach(() => {
  registrar.mockReset();
  corrigir.mockReset();
});

describe("RealizacaoDialog", () => {
  it("Registrar_realizacao_envia_o_horario_efetivo_e_a_versao", async () => {
    registrar.mockResolvedValueOnce(undefined);
    const { user, aoConcluir } = montar();
    expect(screen.getByLabelText(/^Início/)).toHaveValue("08:00");
    expect(screen.getByLabelText(/^Fim/)).toHaveValue("09:00");
    await user.clear(screen.getByLabelText(/^Fim/));
    await user.type(screen.getByLabelText(/^Fim/), "09:30");
    await user.click(screen.getByRole("button", { name: "Registrar realização" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());
    expect(registrar).toHaveBeenCalledWith("c1", { inicio: "2026-09-29T08:00:00-04:00", fim: "2026-09-29T09:30:00-04:00" }, "v1");
  });

  it("Fim_antes_do_inicio_e_recusado_sem_chamar_a_api", async () => {
    const { user } = montar();
    await user.clear(screen.getByLabelText(/^Fim/));
    await user.type(screen.getByLabelText(/^Fim/), "07:00");
    await user.click(screen.getByRole("button", { name: "Registrar realização" }));
    expect(await screen.findByText("O horário de término deve ser posterior ao de início.")).toBeInTheDocument();
    expect(registrar).not.toHaveBeenCalled();
  });

  it.each([
    [409, "Conflito", "Outra pessoa alterou o compromisso."],
    [422, "Regra de negócio", "Só é possível registrar a realização depois do término."],
  ])("Erro_%i_da_api_aparece_no_alerta", async (status, titulo, detalhe) => {
    registrar.mockRejectedValueOnce({ status, titulo, detalhe, errosCampo: {} });
    const { user, aoConcluir } = montar();
    await user.click(screen.getByRole("button", { name: "Registrar realização" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(new RegExp(detalhe.slice(0, 20)));
    expect(aoConcluir).not.toHaveBeenCalled();
  });

  it("Corrigir_realizacao_exige_justificativa_e_envia_versao_e_justificativa", async () => {
    corrigir.mockResolvedValueOnce(undefined);
    const { user, aoConcluir } = montar(vi.fn(), true);
    await user.clear(screen.getByLabelText(/^Início/));
    await user.type(screen.getByLabelText(/^Início/), "08:30");
    await user.click(screen.getByRole("button", { name: "Corrigir realização" }));
    expect(await screen.findByText("Informe a justificativa (mínimo 5 caracteres).")).toBeInTheDocument();
    expect(corrigir).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText(/^Justificativa da correção/), "  Horário anotado errado  ");
    await user.click(screen.getByRole("button", { name: "Corrigir realização" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());
    expect(corrigir).toHaveBeenCalledWith(
      "c1",
      { inicio: "2026-09-29T08:30:00-04:00", fim: "2026-09-29T09:00:00-04:00", justificativa: "Horário anotado errado" },
      "v1",
    );
    expect(registrar).not.toHaveBeenCalled();
  });
});
