// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../../components/ui/Toast";
import type { AjusteProducao } from "../../../lib/types";
import { producaoService } from "../services/producaoService";
import { AjustesTab } from "./AjustesTab";

vi.mock("../services/producaoService", () => ({ producaoService: { listarAjustes: vi.fn(), criarAjuste: vi.fn() } }));
const listar = vi.mocked(producaoService.listarAjustes);

const ajuste = (i: number): AjusteProducao => ({
  id: `a${i}`,
  profissionalId: "p1",
  profissional: "Ana Souza",
  data: "2026-09-21",
  categoria: "ReunioesEquipe",
  rotulo: "Reuniões de equipe",
  ocorrencias: 1,
  minutos: 30,
  justificativa: `Ajuste ${i}`,
  autor: "Coordenação",
  em: "2026-09-22T10:00:00Z",
});

describe("AjustesTab", () => {
  it("Mostra_20_por_pagina_e_avanca", async () => {
    listar.mockResolvedValue(Array.from({ length: 25 }, (_, i) => ajuste(i)));
    render(
      <QueryClientProvider client={new QueryClient()}>
        <ToastProvider>
          <AjustesTab inicio="2026-09-01" fim="2026-09-30" />
        </ToastProvider>
      </QueryClientProvider>,
    );
    expect(await screen.findByText("Mostrando 1–20 de 25")).toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(21);
    await userEvent.setup().click(screen.getByRole("button", { name: "Próxima" }));
    expect(screen.getByText("Mostrando 21–25 de 25")).toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(6);
  });
});
