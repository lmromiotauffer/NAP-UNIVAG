// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../../components/ui/Toast";
import type { SemanaProducao } from "../../../lib/types";
import { producaoService } from "../services/producaoService";
import { SemanasTab } from "./SemanasTab";

vi.mock("../services/producaoService", () => ({ producaoService: { listarSemanas: vi.fn(), fecharSemana: vi.fn(), reabrirSemana: vi.fn() } }));
const listar = vi.mocked(producaoService.listarSemanas);
const fechar = vi.mocked(producaoService.fecharSemana);
const reabrir = vi.mocked(producaoService.reabrirSemana);

const semana = (extra: Partial<SemanaProducao> = {}): SemanaProducao => ({
  profissionalId: "p1",
  profissional: "Ana Souza",
  semana: "2026-09-21",
  fechada: false,
  pendentes: 0,
  versao: null,
  eventos: [],
  ...extra,
});

function montar(semanas: SemanaProducao[], podeReabrir: boolean) {
  listar.mockResolvedValue(semanas);
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <SemanasTab inicio="2026-09-01" fim="2026-09-30" hoje="2026-10-03" podeReabrir={podeReabrir} />
      </ToastProvider>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

beforeEach(() => {
  listar.mockReset();
  fechar.mockReset();
  reabrir.mockReset();
});

describe("SemanasTab", () => {
  it("Fechar_com_pendencias_pede_confirmacao", async () => {
    fechar.mockResolvedValueOnce(undefined);
    const user = montar([semana({ pendentes: 3 })], false);
    await user.click(await screen.findByRole("button", { name: /Fechar semana de/ }));
    const dialogo = within(screen.getByRole("dialog"));
    expect(dialogo.getByText("Esta semana tem 3 pendências de classificação. Fechar mesmo assim?")).toBeInTheDocument();
    expect(fechar).not.toHaveBeenCalled();
    await user.click(dialogo.getByRole("button", { name: "Fechar semana" }));
    await waitFor(() => expect(fechar).toHaveBeenCalledWith({ profissionalId: "p1", semana: "2026-09-21", confirmarPendencias: true }));
  });

  it("Fechar_mostra_o_422_do_servidor_no_alerta", async () => {
    fechar.mockRejectedValueOnce({ status: 422, titulo: "Regra de negócio", detalhe: "A semana tem 3 pendências de classificação.", errosCampo: {} });
    const user = montar([semana({ pendentes: 3 })], false);
    await user.click(await screen.findByRole("button", { name: /Fechar semana de/ }));
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Fechar semana" }));
    expect(await within(screen.getByRole("dialog")).findByRole("alert")).toHaveTextContent("A semana tem 3 pendências de classificação.");
  });

  it("Semana_ainda_nao_encerrada_nao_oferece_fechar", async () => {
    montar([semana({ semana: "2026-09-28" })], false);
    expect(await screen.findByText("Aberta")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Fechar semana/ })).not.toBeInTheDocument();
  });

  it("Profissional_nao_ve_reabrir", async () => {
    montar([semana({ fechada: true, versao: "v1" })], false);
    expect(await screen.findByText("Fechada")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Reabrir/ })).not.toBeInTheDocument();
  });

  it("Reabrir_exige_justificativa_e_envia_a_versao", async () => {
    reabrir.mockResolvedValueOnce(undefined);
    const user = montar([semana({ fechada: true, versao: "v7" })], true);
    await user.click(await screen.findByRole("button", { name: /Reabrir semana de/ }));
    const dialogo = within(screen.getByRole("dialog"));
    await user.click(dialogo.getByRole("button", { name: "Reabrir semana" }));
    expect(await dialogo.findByText(/Informe a justificativa/)).toBeInTheDocument();
    expect(reabrir).not.toHaveBeenCalled();
    await user.type(dialogo.getByLabelText(/^Justificativa/), "Faltou lançar um atendimento");
    await user.click(dialogo.getByRole("button", { name: "Reabrir semana" }));
    await waitFor(() => expect(reabrir).toHaveBeenCalledWith({ profissionalId: "p1", semana: "2026-09-21", justificativa: "Faltou lançar um atendimento" }, "v7"));
  });
});

describe("SemanasTab paginação", () => {
  it("Mostra_20_por_pagina_e_avanca", async () => {
    const semanas = Array.from({ length: 25 }, (_, i) => semana({ profissionalId: `p${i}`, profissional: `Profissional ${i}` }));
    const user = montar(semanas, false);
    expect(await screen.findByText("Mostrando 1–20 de 25")).toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(21);
    await user.click(screen.getByRole("button", { name: "Próxima" }));
    expect(screen.getByText("Mostrando 21–25 de 25")).toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(6);
  });
});
