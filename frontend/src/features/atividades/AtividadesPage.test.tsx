// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { ToastProvider } from "../../components/ui/Toast";
import { responderApi } from "../../test/apiFalsa";
import { AtividadesPage } from "./AtividadesPage";

const item = {
  id: "at1",
  data: "2026-09-29",
  turno: "Manha",
  inicio: "2026-09-29T08:00:00-04:00",
  fim: "2026-09-29T09:30:00-04:00",
  duracaoMinutos: 90,
  cursoId: "cu1",
  curso: "Direito",
  profissionalId: "p1",
  profissional: "Ana Souza",
  tipo: "RodaConversa",
  descricao: "Acolhimento",
  avulsa: false,
  versao: "v1",
};

function montar(perfil: string) {
  responderApi({
    "/atividades": { itens: [item], pagina: 1, tamanho: 20, total: 1 },
    "/cursos": [{ id: "cu1", nome: "Direito", ativo: true, versao: "v" }],
    "/usuarios/profissionais": [{ id: "p1", nome: "Ana Souza" }],
  });
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Teste Silva", email: "t@nap.demo", perfil, cursoId: "cu1", curso: "Direito", deveTrocarSenha: false },
    saiuPorVontade: false,
    entrar: vi.fn(),
    trocarSenha: vi.fn(),
    sair: vi.fn(),
  };
  const router = createMemoryRouter([{ path: "/", element: <AtividadesPage /> }]);
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <SessaoContexto value={sessao}>
          <RouterProvider router={router} />
        </SessaoContexto>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("AtividadesPage", () => {
  it("Coordenador_de_curso_ve_lista_sem_acoes", async () => {
    montar("CoordenadorCurso");
    expect(await screen.findByText(/Roda de conversa/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Nova atividade/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Curso")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Só institucionais")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Profissional")).toBeInTheDocument();
  });

  it("Equipe_nap_ve_nova_atividade_e_os_filtros_de_curso", async () => {
    montar("ProfissionalNap");
    expect(await screen.findByText(/Roda de conversa/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Nova atividade/ })).toBeInTheDocument();
    expect(screen.getByLabelText("Curso")).toBeInTheDocument();
    expect(screen.getByLabelText("Só institucionais")).toBeInTheDocument();
  });
});
