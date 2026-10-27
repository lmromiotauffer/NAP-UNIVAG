import { expect, type APIRequestContext, type Page } from "@playwright/test";

/** Senha dos usuários de demonstração, gerada a cada execução pelo global-setup. */
export function senha(): string {
  const s = process.env.E2E_SENHA;
  if (!s) throw new Error("E2E_SENHA ausente: rode pelo `npm run e2e` (o global-setup gera a senha).");
  return s;
}

/** `senhaUsada` é opcional: o usuário criado na jornada entra primeiro com a temporária, depois com a definida por ele. Com troca pendente a tela é "Defina uma nova senha", sem menu. */
export async function entrar(page: Page, email: string, senhaUsada: string = senha()) {
  await page.goto("/entrar");
  await page.getByLabel("E-mail").fill(email);
  // exact: o botão "Mostrar senha" também casaria com o rótulo parcial.
  await page.getByLabel("Senha", { exact: true }).fill(senhaUsada);
  await page.getByRole("button", { name: "Entrar" }).click();
  await expect(page.getByRole("navigation", { name: "Principal" }).or(page.getByRole("heading", { level: 1, name: "Defina uma nova senha" }))).toBeVisible();
}

export async function sair(page: Page) {
  await page.getByRole("button", { name: "Sair", exact: true }).click();
  await expect(page).toHaveURL(/\/entrar$/);
}

/** Chamada direta à API (pelo proxy do Vite) com o token de quem entrou. */
export async function tokenDe(request: APIRequestContext, email: string): Promise<string> {
  const resposta = await request.post("/api/auth/login", { data: { email, senha: senha() } });
  expect(resposta.ok()).toBe(true);
  return ((await resposta.json()) as { accessToken: string }).accessToken;
}

// Os mesmos helpers de dia civil (Cuiabá) da aplicação, em vez de uma cópia aqui.
export { hojeCivil, somarDias } from "../src/lib/format";
