// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { describe, expect, it, vi } from "vitest";
import type { ErroApi } from "../lib/api";
import { LoginPage } from "./LoginPage";
import { SessaoContexto } from "./useSessao";

async function tentarEntrar(erro: ErroApi): Promise<string> {
  const entrar = vi.fn().mockRejectedValue(erro);
  render(
    <MemoryRouter>
      <SessaoContexto value={{ estado: "anonimo", usuario: null, saiuPorVontade: false, entrar, trocarSenha: vi.fn(), sair: vi.fn() }}>
        <LoginPage />
      </SessaoContexto>
    </MemoryRouter>,
  );
  const user = userEvent.setup();
  await user.type(screen.getByRole("textbox", { name: "E-mail" }), "ana@nap.demo");
  await user.type(screen.getByLabelText("Senha"), "Senha12345");
  await user.click(screen.getByRole("button", { name: "Entrar" }));
  expect(entrar).toHaveBeenCalledWith("ana@nap.demo", "Senha12345");
  return (await screen.findByRole("alert")).textContent ?? "";
}

describe("LoginPage", () => {
  it("credencial_recusada_mostra_texto_fixo_sem_apontar_o_campo", async () => {
    expect(await tentarEntrar({ status: 401, titulo: "E-mail ou senha inválidos", errosCampo: {} })).toBe("E-mail ou senha inválidos.");
  });

  it("servidor_fora_mostra_a_mensagem_real_e_nao_culpa_a_senha", async () => {
    expect(await tentarEntrar({ status: 503, titulo: "Servidor indisponível", errosCampo: {} })).toBe("Servidor indisponível");
  });

  it("limite_de_tentativas_mostra_a_mensagem_real", async () => {
    expect(await tentarEntrar({ status: 429, titulo: "Muitas tentativas. Aguarde um instante e tente novamente.", errosCampo: {} })).toBe(
      "Muitas tentativas. Aguarde um instante e tente novamente.",
    );
  });
});
