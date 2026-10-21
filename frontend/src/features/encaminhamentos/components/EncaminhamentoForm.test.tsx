// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { encaminhamentoService } from "../services/encaminhamentoService";
import { EncaminhamentoForm } from "./EncaminhamentoForm";

vi.mock("../services/encaminhamentoService", () => ({ encaminhamentoService: { criar: vi.fn() } }));

const criar = vi.mocked(encaminhamentoService.criar);
const ALUNO = { id: "11111111-1111-1111-1111-111111111111", nome: "Maria Clara Souza", matricula: "2026.0001" };

function montar() {
  const aoConcluir = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient()}>
      <EncaminhamentoForm aluno={ALUNO} aoConcluir={aoConcluir} />
    </QueryClientProvider>,
  );
  return { aoConcluir, user: userEvent.setup() };
}

beforeEach(() => {
  criar.mockReset();
});

describe("EncaminhamentoForm", () => {
  it("Especialidade_coordenacao_fixa_o_destino", async () => {
    criar.mockResolvedValueOnce({ id: "e1" });
    const { user, aoConcluir } = montar();
    await user.selectOptions(screen.getByLabelText(/Especialidade/), "CoordenacaoCurso");

    const destino = screen.getByLabelText(/Destino/);
    expect(destino).toBeDisabled();
    expect(destino).toHaveDisplayValue("Coordenação do curso");
    expect(screen.getByText("Visível só à equipe NAP.")).toBeInTheDocument();

    await user.type(screen.getByLabelText(/Motivo restrito/), "Queda de rendimento e faltas.");
    await user.type(screen.getByLabelText(/Resumo compartilhável/), "Acompanhar frequência.");
    await user.click(screen.getByRole("button", { name: "Encaminhar" }));

    await waitFor(() => expect(aoConcluir).toHaveBeenCalledWith("e1"));
    expect(criar.mock.calls[0]?.[0]).toMatchObject({ alunoId: ALUNO.id, especialidade: "CoordenacaoCurso", destino: "CoordenacaoCurso", observacoes: null });
  });

  it("Demais_especialidades_oferecem_sip_clinica_e_externo", async () => {
    const { user } = montar();
    await user.selectOptions(screen.getByLabelText(/Especialidade/), "Psicologia");

    const opcoes = Array.from((screen.getByLabelText(/Destino/) as HTMLSelectElement).options).map((o) => o.text);
    expect(opcoes).toEqual(["Selecione", "SIP", "Clínica Escola", "Serviço externo"]);
    await user.selectOptions(screen.getByLabelText(/Destino/), "Sip");
    expect(screen.getByText("Visível só à equipe NAP e ao SIP/Clínica Escola.")).toBeInTheDocument();
  });

  it("Motivo_restrito_obrigatorio_com_mensagem_em_portugues", async () => {
    const { user } = montar();
    expect(screen.getByText("Visível só à equipe NAP e, nos destinos SIP e Clínica Escola, ao serviço de destino.")).toBeInTheDocument();
    expect(screen.getByText("Pode ser lido pela coordenação do curso.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Encaminhar" }));

    expect(await screen.findByText("Informe o motivo restrito (mínimo 3 caracteres).")).toBeInTheDocument();
    expect(criar).not.toHaveBeenCalled();
  });
});

describe("EncaminhamentoForm: validação no primeiro envio", () => {
  it("Destino_ausente_aparece_no_primeiro_envio_junto_dos_erros_de_campo", async () => {
    const { user } = montar();
    await user.click(screen.getByRole("button", { name: "Encaminhar" }));

    expect(await screen.findByText("Selecione o destino.")).toBeInTheDocument();
    expect(screen.getByText("Selecione a especialidade.")).toBeInTheDocument();
    expect(screen.getByText("Informe o motivo restrito (mínimo 3 caracteres).")).toBeInTheDocument();
    expect(criar).not.toHaveBeenCalled();
  });

  it("Ajuda_do_encaminhamento", () => {
    montar();
    expect(screen.getByText("Use encaminhamento quando outro setor precisa agir; orientação interna é um atendimento.")).toBeInTheDocument();
  });
});
