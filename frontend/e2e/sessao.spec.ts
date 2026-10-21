/// <reference lib="dom" />
import { expect, test } from "@playwright/test";
import { entrar, sair } from "./apoio";

test("recarregar mantém a sessão", async ({ page }) => {
  await entrar(page, "coordenacao@nap.demo");
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Alunos" }).click();
  await expect(page.getByRole("heading", { level: 1, name: "Alunos" })).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/alunos$/);
  await expect(page.getByRole("heading", { level: 1, name: "Alunos" })).toBeVisible();
});

test("depois de sair, voltar no navegador não mostra dados", async ({ page }) => {
  await entrar(page, "coordenacao@nap.demo");
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Alunos" }).click();
  const abrir = page.getByRole("link", { name: /^Abrir ficha de / }).first();
  const nome = ((await abrir.getAttribute("aria-label")) ?? "").replace("Abrir ficha de ", "");
  expect(nome.length).toBeGreaterThan(0);
  await expect(page.getByText(nome)).toBeVisible();
  // Abre a ficha: a saída substitui esta entrada por /entrar e o "voltar" cai em /alunos, a lista com dados.
  await page.getByRole("link", { name: `Abrir ficha de ${nome}` }).click();
  await expect(page.getByRole("heading", { level: 1, name: nome })).toBeVisible();

  await sair(page);
  const urlAposSair = page.url();
  const navegacoes: string[] = [];
  page.on("framenavigated", (f) => {
    if (f === page.mainFrame()) navegacoes.push(new URL(f.url()).pathname);
  });
  // Sonda no documento (a SPA não recarrega): marca se o nome aparecer em qualquer renderização, mesmo transitória.
  await page.evaluate((n) => {
    const w = window as unknown as { nomeVisto: boolean };
    w.nomeVisto = false;
    new MutationObserver(() => {
      if (document.body.innerText.includes(n)) w.nomeVisto = true;
    }).observe(document.body, { subtree: true, childList: true, characterData: true });
  }, nome);

  await page.goBack();
  // O voltar teve efeito: passou pela rota protegida e foi devolvido ao login.
  await expect.poll(() => navegacoes).toContain("/alunos");
  await expect(page).toHaveURL(urlAposSair);
  await expect(page.getByRole("heading", { level: 1, name: "Entrar no NAP" })).toBeVisible();
  // Janela curta para pegar dados que chegassem depois do redirecionamento.
  await page.waitForTimeout(1_000);
  await expect(page.getByText(nome)).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { nomeVisto: boolean }).nomeVisto)).toBe(false);
  await expect(page.getByRole("navigation", { name: "Principal" })).toHaveCount(0);
});
