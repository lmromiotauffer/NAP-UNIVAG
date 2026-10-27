// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../../auth/useSessao";
import { ToastProvider } from "../../../components/ui/Toast";
import { administracaoService } from "../services/administracaoService";
import { AutorizacoesForaEscala } from "./AutorizacoesForaEscala";

vi.mock("../services/administracaoService", () => ({
  administracaoService: { listarAutorizacoes: vi.fn(), criarAutorizacao: vi.fn(), revogarAutorizacao: vi.fn(), listarUsuarios: vi.fn() },
}));
vi.mock("../../compartilhado/catalogos", () => ({ useProfissionais: () => ({ data: [{ id: "p1", nome: "Ana Souza" }] }) }));

const autorizacao = (extra: Record<string, unknown> = {}) => ({
  id: "a1", profissionalId: "p1", profissional: "Ana Souza", inicio: "2026-10-10T08:00:00-04:00", fim: "2026-10-10T12:00:00-04:00",
  justificativa: "Plantão de matrícula", autorizadoPorId: "u1", autorizadoPor: "Ada Admin", criadaEm: "2026-10-03T12:00:00Z", revogadaEm: null, ...extra,
});

function montar(perfil = "CoordenadorNap") {
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Quem Opera", email: "q@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
    saiuPorVontade: false, entrar: vi.fn(), trocarSenha: vi.fn(), sair: vi.fn(),
  };
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoContexto value={sessao}>
        <ToastProvider>
          <AutorizacoesForaEscala />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

beforeEach(() => {
  vi.mocked(administracaoService.listarAutorizacoes).mockReset().mockResolvedValue({ itens: [autorizacao()], pagina: 1, tamanho: 20, total: 1 } as never);
  vi.mocked(administracaoService.criarAutorizacao).mockReset();
  vi.mocked(administracaoService.revogarAutorizacao).mockReset();
});

describe("AutorizacoesForaEscala", () => {
  it("Cria_autorizacao_com_justificativa", async () => {
    vi.mocked(administracaoService.criarAutorizacao).mockResolvedValue({ id: "a2" });
    const user = montar();
    expect(await screen.findByText("Plantão de matrícula")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Nova autorização" }));
    const d = within(screen.getByRole("dialog"));
    await user.selectOptions(d.getByLabelText(/^Profissional/), "p1");
    await user.type(d.getByLabelText(/^Início/), "2026-10-11T08:00");
    await user.type(d.getByLabelText(/^Fim/), "2026-10-11T12:00");
    await user.type(d.getByLabelText(/^Justificativa/), "abc");
    await user.click(d.getByRole("button", { name: "Autorizar" }));
    expect(await d.findByText(/mínimo 5 caracteres/)).toBeInTheDocument();
    expect(administracaoService.criarAutorizacao).not.toHaveBeenCalled();

    await user.type(d.getByLabelText(/^Justificativa/), "de");
    await user.click(d.getByRole("button", { name: "Autorizar" }));
    expect(administracaoService.criarAutorizacao).toHaveBeenCalledWith(
      { profissionalId: "p1", inicio: "2026-10-11T08:00:00-04:00", fim: "2026-10-11T12:00:00-04:00", justificativa: "abcde" },
      expect.any(String),
    );
    expect(await screen.findByText("Autorização criada.")).toBeInTheDocument();
  });

  it("Revoga_com_confirmacao", async () => {
    vi.mocked(administracaoService.revogarAutorizacao).mockResolvedValue(undefined);
    const user = montar();
    await user.click(await screen.findByRole("button", { name: /Revogar autorização de Ana Souza/ }));
    expect(administracaoService.revogarAutorizacao).not.toHaveBeenCalled();
    const d = within(screen.getByRole("dialog"));
    expect(d.getByText(/Registros já feitos não são apagados/)).toBeInTheDocument();
    await user.click(d.getByRole("button", { name: "Revogar" }));
    expect(administracaoService.revogarAutorizacao).toHaveBeenCalledWith("a1");
  });

  it("Falha_ao_revogar_fica_no_dialogo", async () => {
    vi.mocked(administracaoService.revogarAutorizacao).mockRejectedValue(new Error("Falha na rede"));
    const user = montar();
    await user.click(await screen.findByRole("button", { name: /Revogar autorização de Ana Souza/ }));
    const d = within(screen.getByRole("dialog"));
    await user.click(d.getByRole("button", { name: "Revogar" }));
    expect(await d.findByRole("alert")).toHaveTextContent("Falha na rede");
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("Profissional_nao_ve_autorizacoes", () => {
    montar("ProfissionalNap");
    expect(screen.queryByText("Autorizações fora da escala")).not.toBeInTheDocument();
    expect(administracaoService.listarAutorizacoes).not.toHaveBeenCalled();
  });
});
