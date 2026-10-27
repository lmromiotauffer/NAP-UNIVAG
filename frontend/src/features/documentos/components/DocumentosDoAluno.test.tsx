// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../../auth/useSessao";
import { ToastProvider } from "../../../components/ui/Toast";
import type { Documento } from "../../../lib/types";
import { documentoService } from "../services/documentoService";
import { DocumentosDoAluno } from "./DocumentosDoAluno";

vi.mock("../services/documentoService", () => ({ documentoService: { listar: vi.fn(), compartilhar: vi.fn(), baixar: vi.fn() } }));

const listar = vi.mocked(documentoService.listar);
const compartilhar = vi.mocked(documentoService.compartilhar);

const documento: Documento = {
  id: "d1",
  categoria: "Laudo",
  descricao: "Laudo neuropsicológico",
  nomeArquivo: "laudo.pdf",
  tamanhoBytes: 1258291,
  autor: "Ana Souza",
  criadoEm: "2026-09-20T10:00:00-04:00",
  visibilidade: "RestritoNap",
  compartilhadoPor: null,
  compartilhadoEm: null,
  arquivado: false,
  versao: "v1",
};

function montar(perfil: string) {
  listar.mockResolvedValue({ itens: [documento], pagina: 1, tamanho: 20, total: 1 });
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Helena Prado", email: "h@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
    saiuPorVontade: false,
    entrar: vi.fn(),
    trocarSenha: vi.fn(),
    sair: vi.fn(),
  };
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoContexto value={sessao}>
        <ToastProvider>
          <DocumentosDoAluno alunoId="a1" />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  listar.mockReset();
  compartilhar.mockReset();
});

describe("DocumentosDoAluno", () => {
  it("Coordenador_nao_ve_acoes_de_gestao", async () => {
    montar("CoordenadorCurso");
    expect(await screen.findByText(/laudo\.pdf · 1,2 MB/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Baixar/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Anexar documento" })).toBeInTheDocument();
    for (const nome of [/Alterar categoria/, /Compartilhar/, /Arquivar/]) expect(screen.queryByRole("button", { name: nome })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Mostrar arquivados")).not.toBeInTheDocument();
  });

  it("Profissional_gerencia_mas_nao_ve_filtro_de_arquivados", async () => {
    montar("ProfissionalNap");
    expect(await screen.findByRole("button", { name: /Compartilhar Laudo/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Anexar documento" })).toBeInTheDocument();
    expect(screen.queryByLabelText("Mostrar arquivados")).not.toBeInTheDocument();
  });

  it("Coordenacao_nap_pede_arquivados_pelo_filtro", async () => {
    montar("CoordenadorNap");
    await screen.findByText("laudo.pdf", { exact: false });
    await userEvent.setup().click(screen.getByLabelText("Mostrar arquivados"));
    await waitFor(() => expect(listar).toHaveBeenLastCalledWith("a1", { pagina: 1, tamanho: 10, incluirArquivados: true }));
  });

  it("Compartilhar_exige_justificativa", async () => {
    compartilhar.mockResolvedValue(undefined);
    montar("ProfissionalNap");
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /Compartilhar Laudo/ }));
    await user.click(screen.getByRole("button", { name: "Compartilhar" }));
    expect(await screen.findByText("Informe a justificativa (de 3 a 500 caracteres).")).toBeInTheDocument();
    expect(compartilhar).not.toHaveBeenCalled();

    await user.type(screen.getByLabelText(/Justificativa/), "Coordenação precisa do laudo.");
    await user.click(screen.getByRole("button", { name: "Compartilhar" }));
    await waitFor(() => expect(compartilhar).toHaveBeenCalledWith("d1", { justificativa: "Coordenação precisa do laudo." }, "v1"));
  });
});
