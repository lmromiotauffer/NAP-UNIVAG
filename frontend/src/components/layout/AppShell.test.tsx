// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { responderApi } from "../../test/apiFalsa";
import { AppShell } from "./AppShell";

const sessao = (perfil: string): ContextoSessao => ({
  estado: "autenticado",
  usuario: { id: "u1", nome: "Teste Silva", email: "t@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
});

function montar(perfil: string, naoLidas: number, mensagens = 0) {
  responderApi({ "/notificacoes/contagem": { naoLidas }, "/comunicacoes/contagem": { naoLidas: mensagens } });
  const router = createMemoryRouter([{ path: "/", element: <AppShell />, children: [{ index: true, element: <p>Início</p> }] }]);
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoContexto value={sessao(perfil)}>
        <RouterProvider router={router} />
      </SessaoContexto>
    </QueryClientProvider>,
  );
}

describe("AppShell: comunicações", () => {
  it("Contador_mostra_nao_lidas_com_nome_acessivel", async () => {
    montar("Sip", 3);
    // Trilho e barra inferior coexistem no DOM (a visibilidade é por CSS).
    expect((await screen.findAllByText("3 não lidas")).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("link", { name: /3 não lidas/ }).length).toBeGreaterThan(0);
  });

  it("Contador_acima_de_99_mostra_99_mais_e_mantem_o_total_no_nome", async () => {
    montar("CoordenadorCurso", 150);
    expect((await screen.findAllByText("99+")).length).toBeGreaterThan(0);
    expect(screen.getAllByText("150 não lidas").length).toBeGreaterThan(0);
  });

  it("Sem_nao_lidas_nao_mostra_selo", async () => {
    montar("ProfissionalNap", 0);
    expect((await screen.findAllByRole("link", { name: "Comunicações" })).length).toBeGreaterThan(0);
    expect(screen.queryByText(/não lidas/)).not.toBeInTheDocument();
  });

  it("Contador_soma_mensagens_e_notificacoes", async () => {
    montar("ProfissionalNap", 2, 3);
    expect((await screen.findAllByText("5 não lidas")).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("link", { name: /Comunicações/ })[0]).toHaveAttribute("href", "/comunicacoes");
    expect(screen.queryByRole("link", { name: /Notificações/ })).not.toBeInTheDocument();
  });

  it("Gestao_nao_ve_comunicacoes", async () => {
    montar("Gestao", 4, 4);
    expect((await screen.findAllByRole("link", { name: "Painel" })).length).toBeGreaterThan(0);
    expect(screen.queryByRole("link", { name: /Comunicações|Notificações/ })).not.toBeInTheDocument();
    expect(screen.queryByText(/não lidas/)).not.toBeInTheDocument();
  });
});

describe("AppShell: barra inferior", () => {
  it("Administracao_fica_no_menu_mais_e_a_barra_tem_no_maximo_sete_botoes", async () => {
    montar("Administrador", 0);
    const barra = (await screen.findByRole("button", { name: "Mais" })).closest("nav") as HTMLElement;
    expect(barra.querySelectorAll(":scope > a, :scope > button").length).toBeLessThanOrEqual(7);
    expect(barra.querySelector(":scope > a[href='/administracao']")).toBeNull();
    expect(barra.querySelector("#menu-mais a[href='/administracao']")).not.toBeNull();
    // O trilho (desktop) continua com o item.
    expect(screen.getAllByRole("link", { name: "Administração" })).toHaveLength(1);
    // Atividades segue o mesmo caminho: sem ele a equipe NAP passaria de sete botões.
    expect(barra.querySelector(":scope > a[href='/atividades']")).toBeNull();
    expect(barra.querySelector("#menu-mais a[href='/atividades']")).not.toBeNull();
    // Produção também fica no menu Mais: a equipe NAP passaria de sete botões.
    expect(barra.querySelector(":scope > a[href='/producao']")).toBeNull();
    expect(barra.querySelector("#menu-mais a[href='/producao']")).not.toBeNull();
    // Equipe NAP: Encaminhamentos segue no "Mais".
    expect(barra.querySelector(":scope > a[href='/encaminhamentos']")).toBeNull();
    expect(barra.querySelector("#menu-mais a[href='/encaminhamentos']")).not.toBeNull();
  });

  it.each(["Sip", "CoordenadorCurso"])("Encaminhamentos_fica_na_barra_de_%s", async (perfil) => {
    montar(perfil, 0);
    const barra = (await screen.findByRole("button", { name: "Mais" })).closest("nav") as HTMLElement;
    expect(barra.querySelector(":scope > a[href='/encaminhamentos']")).not.toBeNull();
    expect(barra.querySelector("#menu-mais a[href='/encaminhamentos']")).toBeNull();
    expect(barra.querySelectorAll(":scope > a, :scope > button").length).toBeLessThanOrEqual(6);
  });
});
