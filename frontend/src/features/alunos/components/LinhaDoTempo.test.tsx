// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { alunoService } from "../services/alunoService";
import { LinhaDoTempo } from "./LinhaDoTempo";

vi.mock("../services/alunoService", () => ({ alunoService: { linhaDoTempo: vi.fn() } }));

const TAMANHO = 10;

/** Página por deslocamento, como a API: a página 3 de 25 eventos traz os 5 últimos. */
function paginar(total: number) {
  vi.mocked(alunoService.linhaDoTempo).mockImplementation(async (_id, pagina) => ({
    itens: Array.from({ length: Math.max(0, Math.min(TAMANHO, total - (pagina - 1) * TAMANHO)) }, (_, i) => {
      const n = (pagina - 1) * TAMANHO + i + 1;
      return { em: `2026-01-${String(31 - n).padStart(2, "0")}T12:00:00Z`, tipo: "situacao", titulo: `Evento ${n}`, resumo: null, autor: "Ana" };
    }),
    pagina,
    tamanho: TAMANHO,
    total,
  }));
}

function montar() {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <LinhaDoTempo alunoId="a1" />
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

describe("LinhaDoTempo", () => {
  it("carregar_mais_some_na_ultima_pagina_e_os_eventos_ficam_em_ordem", async () => {
    paginar(25);
    const user = montar();
    await user.click(await screen.findByRole("button", { name: "Carregar mais" }));
    await screen.findByText("Evento 20");
    await user.click(screen.getByRole("button", { name: "Carregar mais" }));
    await screen.findByText("Evento 25");

    expect(screen.queryByRole("button", { name: "Carregar mais" })).not.toBeInTheDocument();
    const titulos = screen.getAllByText(/^Evento \d+$/).map((e) => e.textContent);
    expect(titulos).toEqual(Array.from({ length: 25 }, (_, i) => `Evento ${i + 1}`));
    expect(alunoService.linhaDoTempo).toHaveBeenCalledTimes(3);
  });

  it("sem_segunda_pagina_nao_mostra_carregar_mais", async () => {
    paginar(7);
    montar();
    await screen.findByText("Evento 7");
    expect(screen.queryByRole("button", { name: "Carregar mais" })).not.toBeInTheDocument();
  });
});
