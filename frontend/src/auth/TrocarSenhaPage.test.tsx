// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosAdapter } from "axios";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, describe, expect, it } from "vitest";
import { ToastProvider } from "../components/ui/Toast";
import { api, definirToken } from "../lib/api";
import { RotaProtegida } from "./RotaProtegida";
import { SessaoProvider } from "./SessaoProvider";
import { TrocarSenhaPage } from "./TrocarSenhaPage";

const adapterOriginal = api.defaults.adapter;
const usuario = (deveTrocarSenha: boolean) => ({ id: "u1", nome: "Rafael Torres", email: "r@nap.demo", perfil: "ProfissionalNap", cursoId: null, curso: null, deveTrocarSenha });

afterEach(() => {
  api.defaults.adapter = adapterOriginal;
  definirToken(null);
});

function montar(aoTrocar: (corpo: unknown) => { status: number; data: unknown }) {
  const chamadas: string[] = [];
  const adapter: AxiosAdapter = async (config) => {
    chamadas.push(`${config.method} ${config.url}`);
    let r: { status: number; data: unknown };
    if (config.url === "/auth/refresh") r = { status: 200, data: { accessToken: "t1", usuario: usuario(true) } };
    else if (config.url === "/auth/senha") r = aoTrocar(JSON.parse(String(config.data)));
    else r = { status: 404, data: {} };
    const resposta = { status: r.status, statusText: "", data: r.data, headers: {}, config };
    if (r.status >= 400) throw new AxiosError("falhou", String(r.status), config, null, resposta);
    return resposta;
  };
  api.defaults.adapter = adapter;
  const router = createMemoryRouter(
    [
      { path: "/definir-senha", element: <RotaProtegida><TrocarSenhaPage /></RotaProtegida> },
      { path: "/", element: <RotaProtegida><p>Painel liberado</p></RotaProtegida> },
    ],
    { initialEntries: ["/definir-senha"] },
  );
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoProvider>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessaoProvider>
    </QueryClientProvider>,
  );
  return { router, chamadas, user: userEvent.setup() };
}

async function preencher(user: ReturnType<typeof userEvent.setup>, atual: string, nova: string, confirmacao: string) {
  await user.type(await screen.findByLabelText("Senha atual"), atual);
  await user.type(screen.getByLabelText("Nova senha"), nova);
  await user.type(screen.getByLabelText("Confirmar nova senha"), confirmacao);
  await user.click(screen.getByRole("button", { name: "Salvar nova senha" }));
}

describe("TrocarSenhaPage", () => {
  it("Troca_atualiza_a_sessao_e_libera_o_app", async () => {
    const { router, chamadas, user } = montar((corpo) => {
      expect(corpo).toEqual({ senhaAtual: "Temporaria12", novaSenha: "NovaSenha123" });
      return { status: 200, data: { accessToken: "t2", usuario: usuario(false) } };
    });
    expect(await screen.findByRole("heading", { name: "Defina uma nova senha" })).toBeInTheDocument();
    await preencher(user, "Temporaria12", "NovaSenha123", "NovaSenha123");

    expect(await screen.findByText("Painel liberado")).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("Senha alterada.");
    expect(router.state.location.pathname).toBe("/");
    expect(chamadas).toContain("post /auth/senha");
  });

  it("Valida_confirmacao_e_regras_antes_de_enviar", async () => {
    const { chamadas, user } = montar(() => ({ status: 200, data: {} }));
    await preencher(user, "x", "curta", "outra");
    expect(await screen.findByText("A nova senha deve ter no mínimo 10 caracteres.")).toBeInTheDocument();
    expect(screen.getByText("A confirmação deve ser igual à nova senha.")).toBeInTheDocument();
    expect(chamadas).not.toContain("post /auth/senha");
  });

  it("Erro_do_servidor_aparece_no_campo", async () => {
    const { user } = montar(() => ({ status: 400, data: { title: "Erro", errors: { novaSenha: ["A nova senha deve ser diferente da atual."] } } }));
    await preencher(user, "Temporaria12", "Temporaria12", "Temporaria12");
    expect(await screen.findByText("A nova senha deve ser diferente da atual.")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("button", { name: "Salvar nova senha" })).toBeEnabled());
  });
});
