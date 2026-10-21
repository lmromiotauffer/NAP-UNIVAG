// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { expect, it, vi } from "vitest";
import { hojeCivil } from "../../../lib/format";
import type { EncaminhamentoLista } from "../../../lib/types";
import { encaminhamentoService } from "../services/encaminhamentoService";
import { ListaEncaminhamentos } from "./ListaEncaminhamentos";

vi.mock("../services/encaminhamentoService", () => ({ encaminhamentoService: { listar: vi.fn() } }));

const item: EncaminhamentoLista = {
  id: "e1",
  alunoId: "a1",
  aluno: "Maria Clara Souza",
  matricula: "2026.0001",
  curso: "Direito",
  especialidade: null,
  destino: null,
  estado: "Encaminhado",
  data: hojeCivil(),
  solicitante: "Ana Souza",
  semAcolhimentoNoPrazo: false,
  atualizadoEm: "2026-09-20T10:00:00-04:00",
};

it("Especialidade_e_destino_ocultos_mostram_rotulo_neutro", async () => {
  vi.mocked(encaminhamentoService.listar).mockResolvedValue({ itens: [item], pagina: 1, tamanho: 20, total: 1 });
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <ListaEncaminhamentos filtro={{ pagina: 1 }} comFiltro={false} vazio={{ titulo: "", descricao: "" }} aoMudarPagina={vi.fn()} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  await screen.findAllByText("Maria Clara Souza");
  expect(screen.getAllByText(/Não compartilhado com o curso/).length).toBeGreaterThan(0);
  expect(screen.getByRole("link", { name: /Não compartilhado com o curso/ })).toBeInTheDocument();
});
