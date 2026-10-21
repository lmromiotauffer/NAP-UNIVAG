// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../../auth/useSessao";
import { ToastProvider } from "../../../components/ui/Toast";
import { hojeCivil, somarDias } from "../../../lib/format";
import { AdministracaoPage } from "../AdministracaoPage";
import { administracaoService } from "../services/administracaoService";
import { AuditoriaTab } from "./AuditoriaTab";

vi.mock("../services/administracaoService", () => ({ administracaoService: { listarAuditoria: vi.fn(), listarUsuarios: vi.fn() } }));

const evento = (id: string, extra: Record<string, unknown> = {}) => ({
  id, em: "2026-10-02T15:30:00Z", usuarioId: "u1", usuario: "Ada Admin", acao: "alunos.consultar", recurso: "Aluno", recursoId: null, resultado: "sucesso", ...extra,
});
const pagina = (itens: unknown[], total = itens.length, n = 1) => ({ itens, pagina: n, tamanho: 20, total });

function montar(elemento: React.ReactNode, perfil = "CoordenadorNap") {
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Quem Opera", email: "q@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
    saiuPorVontade: false, entrar: vi.fn(), trocarSenha: vi.fn(), sair: vi.fn(),
  };
  const router = createMemoryRouter([{ path: "/", element: elemento }]);
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoContexto value={sessao}>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

const listar = () => vi.mocked(administracaoService.listarAuditoria);

beforeEach(() => {
  vi.mocked(administracaoService.listarUsuarios).mockResolvedValue({ itens: [{ id: "u1", nome: "Ada Admin" }], pagina: 1, tamanho: 100, total: 1 } as never);
  listar().mockReset();
});

describe("AuditoriaTab", () => {
  it("Auditoria_filtra_e_pagina", async () => {
    listar().mockImplementation(async (f) => pagina([evento(`e${f.pagina}`)], 45, f.pagina) as never);
    const user = montar(<AuditoriaTab />);
    await screen.findByText("Mostrando 1–20 de 45");
    const hoje = hojeCivil();
    // Abre com os últimos 7 dias e sempre manda inicio e fim.
    expect(listar()).toHaveBeenLastCalledWith(expect.objectContaining({ inicio: somarDias(hoje, -6), fim: hoje, pagina: 1 }));

    await user.click(screen.getByRole("button", { name: "Próxima" }));
    await screen.findByText(/Mostrando 21–40/);
    expect(listar()).toHaveBeenLastCalledWith(expect.objectContaining({ pagina: 2 }));

    await user.selectOptions(await screen.findByRole("option", { name: "Ada Admin" }).then(() => screen.getByLabelText("Usuário")), "u1");
    await user.type(screen.getByLabelText("Ação"), "login");
    await user.selectOptions(screen.getByLabelText("Resultado"), "negado");
    await user.click(screen.getByRole("button", { name: "Filtrar" }));
    await screen.findByText("Mostrando 1–20 de 45");
    expect(listar()).toHaveBeenLastCalledWith(expect.objectContaining({ usuarioId: "u1", acao: "login", resultado: "negado", pagina: 1, inicio: somarDias(hoje, -6), fim: hoje }));
  });

  it("Fim_antes_do_inicio_nao_consulta", async () => {
    listar().mockResolvedValue(pagina([evento("e1")]) as never);
    const user = montar(<AuditoriaTab />);
    await screen.findByText("Ada Admin", { selector: "td" });
    const chamadas = listar().mock.calls.length;
    const fim = screen.getByLabelText("Até");
    await user.clear(fim);
    await user.type(fim, "2020-01-01");
    expect(await screen.findByRole("alert")).toHaveTextContent("A data final não pode ser anterior à inicial.");
    expect(screen.getByRole("button", { name: "Filtrar" })).toBeDisabled();
    expect(listar().mock.calls.length).toBe(chamadas);
  });

  it("Periodo_acima_de_366_dias_nao_consulta", async () => {
    listar().mockResolvedValue(pagina([evento("e1")]) as never);
    const user = montar(<AuditoriaTab />);
    await screen.findByText("Ada Admin", { selector: "td" });
    const chamadas = listar().mock.calls.length;
    const inicio = screen.getByLabelText("De");
    await user.clear(inicio);
    await user.type(inicio, "2024-01-01");
    expect(await screen.findByRole("alert")).toHaveTextContent("O período não pode passar de 366 dias.");
    expect(screen.getByRole("button", { name: "Filtrar" })).toBeDisabled();
    expect(listar().mock.calls.length).toBe(chamadas);
  });

  it("Negado_aparece_destacado_e_usuario_ausente_vira_sistema_ou_nao_identificado", async () => {
    listar().mockResolvedValue(
      pagina([evento("e1", { resultado: "negado", acao: "acesso.negado" }), evento("e2", { usuarioId: null, usuario: null, acao: "login.falha", resultado: "negado" }), evento("e3", { usuarioId: "x", usuario: null }), evento("e4", { usuarioId: null, usuario: null, acao: "job.limpeza" })]) as never,
    );
    montar(<AuditoriaTab />);
    await screen.findByText("acesso.negado");
    const tabela = screen.getByRole("table", { name: "Trilha de auditoria" });
    const linhas = within(tabela).getAllByRole("row").slice(1);
    expect(within(linhas[0]!).getByText("negado")).toHaveClass("bg-urgente-fundo");
    expect(within(linhas[0]!).getByText("02/10/2026 11h30")).toBeInTheDocument();
    // Sem usuário: negado = tentativa sem sessão; sucesso = sistema. Com id e sem nome = não identificado.
    expect(within(linhas[1]!).getByText("Não identificado")).toBeInTheDocument();
    expect(within(linhas[2]!).getByText("Não identificado")).toBeInTheDocument();
    expect(within(linhas[3]!).getByText("Sistema")).toBeInTheDocument();
    expect(within(linhas[3]!).getByText("sucesso")).not.toHaveClass("bg-urgente-fundo");
  });
});

describe("AdministracaoPage", () => {
  it("Profissional_nao_ve_a_aba_auditoria", () => {
    montar(<AdministracaoPage />, "ProfissionalNap");
    expect(screen.queryByRole("tab", { name: "Auditoria" })).not.toBeInTheDocument();
  });

  it("Coordenacao_ve_a_aba_auditoria", () => {
    montar(<AdministracaoPage />, "CoordenadorNap");
    expect(screen.getByRole("tab", { name: "Auditoria" })).toBeInTheDocument();
  });
});
