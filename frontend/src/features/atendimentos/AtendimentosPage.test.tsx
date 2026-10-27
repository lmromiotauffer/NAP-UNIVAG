// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { ToastProvider } from "../../components/ui/Toast";
import { responderApi } from "../../test/apiFalsa";
import { AtendimentosPage } from "./AtendimentosPage";
import { atendimentoService } from "./services/atendimentoService";

vi.mock("./services/atendimentoService", () => ({
  atendimentoService: { listar: vi.fn().mockResolvedValue({ itens: [], pagina: 1, tamanho: 20, total: 0 }), registrar: vi.fn() },
}));
responderApi({
  "/usuarios/profissionais": [{ id: "22222222-2222-2222-2222-222222222222", nome: "Ana Souza", perfil: "ProfissionalNap" }],
});

const sessao: ContextoSessao = {
  estado: "autenticado",
  usuario: { id: "u", nome: "Ana Souza", email: "a@nap.demo", perfil: "ProfissionalNap", cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
};

function montar(url: string) {
  const router = createMemoryRouter([{ path: "/atendimentos", element: <AtendimentosPage /> }], { initialEntries: [url] });
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoContexto value={sessao}>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
}

describe("AtendimentosPage", () => {
  it("filtros_invalidos_da_url_sao_descartados_e_os_validos_vao_para_a_api", async () => {
    montar("/atendimentos?estado=Bogus&de=2026-13-99&tipo=Acompanhamento&pagina=0");
    await waitFor(() => expect(atendimentoService.listar).toHaveBeenCalled());
    // toEqual ignora chaves undefined: estado, de e pagina inválidos não chegam ao filtro.
    expect(vi.mocked(atendimentoService.listar).mock.calls[0]?.[0]).toEqual({ tipo: "Acompanhamento" });
    expect(screen.getByLabelText("Tipo")).toHaveValue("Acompanhamento");
    expect(screen.getByLabelText("Estado")).toHaveValue("");
  });
});
