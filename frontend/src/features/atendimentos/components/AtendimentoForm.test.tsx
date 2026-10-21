// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Compromisso } from "../../../lib/types";
import { atendimentoService } from "../services/atendimentoService";
import { AtendimentoForm } from "./AtendimentoForm";

vi.mock("../services/atendimentoService", () => ({ atendimentoService: { registrar: vi.fn() } }));

const registrar = vi.mocked(atendimentoService.registrar);

const compromisso: Compromisso = {
  id: "c1",
  tipo: "Atendimento",
  titulo: "Atendimento",
  inicio: "2026-09-29T08:00:00-04:00",
  fim: "2026-09-29T09:00:00-04:00",
  profissionalId: "p1",
  profissional: "Ana Souza",
  alunoId: "a1",
  aluno: "Maria Clara Souza",
  estado: "Agendado",
  estadoAtendimento: null,
  remarcadoDeId: null,
  descricao: null,
  versao: "v1",
  grupoId: null,
  participantes: null,
  podeGerirGrupo: false,
};

function montar() {
  const aoConcluir = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient()}>
      <AtendimentoForm compromisso={compromisso} aoConcluir={aoConcluir} />
    </QueryClientProvider>,
  );
  return { aoConcluir, user: userEvent.setup() };
}

beforeEach(() => {
  registrar.mockReset();
});

describe("AtendimentoForm", () => {
  it("ausencia_nao_envia_campos_clinicos", async () => {
    registrar.mockResolvedValueOnce({ id: "at1" });
    const { user, aoConcluir } = montar();
    await user.selectOptions(screen.getByLabelText(/Tipo/), "Acompanhamento");
    await user.type(screen.getByLabelText(/Demanda principal/), "Ansiedade em provas");
    await user.type(screen.getByLabelText(/Observações privadas/), "Nota sigilosa");
    await user.click(screen.getByRole("radio", { name: "Ausente" }));

    expect(screen.queryByLabelText(/Demanda principal/)).not.toBeInTheDocument();
    expect(screen.getByText("Ausência não exige demanda ou nota clínica.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Registrar" }));

    await waitFor(() => expect(aoConcluir).toHaveBeenCalledWith("at1"));
    const [corpo, chave] = registrar.mock.calls[0] ?? [];
    expect(corpo).toMatchObject({ compromissoId: "c1", estado: "Ausente", demandaPrincipal: null, observacoesPrivadas: null, recomendacoesEncaminhamento: null, proximosPassos: null });
    expect(chave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it("realizado_sem_demanda_mostra_erro", async () => {
    const { user } = montar();
    await user.click(screen.getByRole("button", { name: "Registrar" }));

    expect(await screen.findByText("Informe a demanda principal.")).toBeInTheDocument();
    expect(screen.getByLabelText(/Demanda principal/)).toHaveAttribute("aria-invalid", "true");
    expect(registrar).not.toHaveBeenCalled();
  });

  it("Rotulo_de_orientacao_interna", async () => {
    const { user } = montar();
    await user.selectOptions(screen.getByLabelText(/^Tipo/), "OrientacaoAcademica");
    expect(screen.getByRole("option", { name: "Orientação acadêmica" })).toBeInTheDocument();
    expect(screen.getByText("Registro interno do NAP; não gera retorno de outro setor")).toBeInTheDocument();
  });
});
