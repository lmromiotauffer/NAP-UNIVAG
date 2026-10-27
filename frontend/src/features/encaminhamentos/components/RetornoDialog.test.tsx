// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type InternalAxiosRequestConfig } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { api } from "../../../lib/api";
import type { EncaminhamentoDetalhe } from "../../../lib/types";
import { RetornoDialog } from "./RetornoDialog";

const encaminhamento: EncaminhamentoDetalhe = {
  id: "e1",
  alunoId: "a1",
  aluno: "Maria Clara Souza",
  matricula: "2026.0001",
  curso: "Direito",
  especialidade: "Psicologia",
  destino: "Sip",
  estado: "Encaminhado",
  data: "2026-09-20",
  solicitante: "Ana Souza",
  semAcolhimentoNoPrazo: true,
  atualizadoEm: "2026-09-20T10:00:00-04:00",
  solicitanteId: "p1",
  motivoRestrito: "Crises de ansiedade.",
  resumoCompartilhavel: "Acompanhamento psicológico.",
  observacoes: null,
  dataAcolhimento: null,
  dataPrimeiroAtendimento: null,
  dataEncerramento: null,
  podeRegistrarRetorno: true,
  podeCancelar: true,
  evolucoes: [],
  versao: "v1",
};

let pedidos: InternalAxiosRequestConfig[] = [];
let respostas: number[] = [];

beforeEach(() => {
  pedidos = [];
  respostas = [];
  api.defaults.adapter = async (config) => {
    pedidos.push(config);
    const status = respostas.shift() ?? 204;
    const resposta = { status, statusText: "", data: status === 409 ? { title: "Conflito" } : "", headers: {}, config };
    if (status >= 400) throw new AxiosError("falha", undefined, config, undefined, resposta);
    return resposta;
  };
});

describe("RetornoDialog", () => {
  it("Retorno_envia_if_match_da_versao_aberta", async () => {
    respostas = [409, 204];
    const aoConcluir = vi.fn();
    const cliente = new QueryClient();
    const arvore = (e: EncaminhamentoDetalhe) => (
      <QueryClientProvider client={cliente}>
        <RetornoDialog encaminhamento={e} acao="Acolhimento" aoFechar={vi.fn()} aoConcluir={aoConcluir} />
      </QueryClientProvider>
    );
    const { rerender } = render(arvore(encaminhamento));
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Registrar acolhimento" }));
    expect(await screen.findByText("Conflito")).toBeInTheDocument();

    // O detalhe refetchado traz versão nova; o diálogo continua com a versão de quando abriu.
    rerender(arvore({ ...encaminhamento, versao: "v2" }));
    await user.click(screen.getByRole("button", { name: "Registrar acolhimento" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());

    expect(pedidos.map((p) => p.headers["If-Match"])).toEqual(['"v1"', '"v1"']);
    expect(pedidos[0]?.url).toBe("/encaminhamentos/e1/retornos");
    expect(JSON.parse(String(pedidos[0]?.data))).toEqual({ acao: "Acolhimento", data: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/), texto: null });
  });

  it("Evolucao_exige_texto_e_nao_envia_data", async () => {
    const aoConcluir = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <RetornoDialog encaminhamento={{ ...encaminhamento, estado: "Acolhido" }} acao="Evolucao" aoFechar={vi.fn()} aoConcluir={aoConcluir} />
      </QueryClientProvider>,
    );
    const user = userEvent.setup();
    expect(screen.queryByLabelText(/Data/)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Registrar evolução" }));
    expect(await screen.findByText("Descreva a evolução.")).toBeInTheDocument();
    expect(pedidos).toHaveLength(0);

    await user.type(screen.getByLabelText(/Evolução/), "Segue em atendimento semanal.");
    await user.click(screen.getByRole("button", { name: "Registrar evolução" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());
    expect(JSON.parse(String(pedidos[0]?.data))).toEqual({ acao: "Evolucao", data: null, texto: "Segue em atendimento semanal." });
  });
  it.each([
    ["InicioAtendimento", "22/09/2026", "Registrar início do atendimento"],
    ["Alta", "25/09/2026", "Registrar alta"],
    ["Desistencia", "25/09/2026", "Registrar desistência"],
  ] as const)("Data_anterior_a_minima_e_recusada_em_%s", async (acao, minima, botao) => {
    const aoConcluir = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <RetornoDialog
          encaminhamento={{ ...encaminhamento, estado: "EmAtendimento", dataAcolhimento: "2026-09-22", dataPrimeiroAtendimento: "2026-09-25" }}
          acao={acao}
          aoFechar={vi.fn()}
          aoConcluir={aoConcluir}
        />
      </QueryClientProvider>,
    );
    const user = userEvent.setup();
    const data = screen.getByLabelText(/^Data/);
    await user.clear(data);
    await user.type(data, "2026-09-21");
    if (acao === "Desistencia") await user.type(screen.getByLabelText(/Justificativa/), "Mudou de cidade.");
    await user.click(screen.getByRole("button", { name: botao }));

    expect(await screen.findByText(`A data não pode ser anterior a ${minima}.`)).toBeInTheDocument();
    expect(pedidos).toHaveLength(0);
    expect(aoConcluir).not.toHaveBeenCalled();
  });
});
