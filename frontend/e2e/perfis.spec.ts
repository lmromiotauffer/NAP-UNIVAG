import { expect, test, type APIRequestContext } from "@playwright/test";
import { entrar, tokenDe } from "./apoio";

type AlunoLista = { id: string; nome: string; curso: string };

/** Alunos vistos pela coordenação do NAP (todos os cursos). */
async function alunosDaCoordenacao(request: APIRequestContext): Promise<AlunoLista[]> {
  const token = await tokenDe(request, "coordenacao@nap.demo");
  const resposta = await request.get("/api/alunos", { params: { tamanho: 100 }, headers: { Authorization: `Bearer ${token}` } });
  expect(resposta.ok()).toBe(true);
  return ((await resposta.json()) as { itens: AlunoLista[] }).itens;
}

test("coordenação de curso vê só alunos de Direito, sem totais nem agenda", async ({ page, request }) => {
  const outro = (await alunosDaCoordenacao(request)).find((a) => a.curso !== "Direito");
  expect(outro).toBeDefined();

  await entrar(page, "coord.direito@nap.demo");
  const nav = page.getByRole("navigation", { name: "Principal" });
  await expect(nav.getByRole("link", { name: "Alunos" })).toBeVisible();
  await expect(nav.getByRole("link", { name: "Agenda" })).toHaveCount(0);
  await expect(nav.getByRole("link", { name: "Atividades" })).toBeVisible();
  await expect(nav.getByRole("link", { name: "Produção" })).toBeVisible();

  await nav.getByRole("link", { name: "Alunos" }).click();
  const linhas = page.getByRole("table", { name: "Alunos" }).locator("tbody tr");
  await expect(linhas.first()).toBeVisible();
  for (const linha of await linhas.all()) {
    const celulas = linha.getByRole("cell");
    await expect(celulas.nth(2)).toHaveText("Direito");
    // Atend. e Faltas: sem dado clínico para a coordenação de curso.
    await expect(celulas.nth(5)).toHaveText("—");
    await expect(celulas.nth(6)).toHaveText("—");
  }

  await page.goto(`/alunos/${outro?.id}`);
  await expect(page.getByRole("alert").filter({ hasText: /não encontrad/i })).toBeVisible();
  await expect(page.getByRole("heading", { level: 1, name: outro?.nome })).toHaveCount(0);
});

test("gestão vê só Painel e Produção, sem nomes de alunos", async ({ page, request }) => {
  const nomes = (await alunosDaCoordenacao(request)).map((a) => a.nome);
  expect(nomes.length).toBeGreaterThan(0);

  await entrar(page, "gestao@nap.demo");
  const nav = page.getByRole("navigation", { name: "Principal" });
  await expect(nav.getByRole("link")).toHaveCount(2);
  await expect(nav.getByRole("link", { name: "Painel" })).toBeVisible();
  await expect(nav.getByRole("link", { name: "Produção" })).toBeVisible();
  await expect(nav.getByRole("link", { name: /Comunicações/ })).toHaveCount(0);
  // Indicadores agregados carregados: só então o texto da página vale para a checagem.
  await expect(page.getByText("Taxa de ausência")).toBeVisible();
  await expect(page.getByLabel("Carregando painel")).toHaveCount(0);

  const texto = await page.getByRole("main").innerText();
  for (const nome of nomes) expect(texto).not.toContain(nome);
});

test("SIP vê o painel de encaminhamentos, sem Alunos", async ({ page }) => {
  await entrar(page, "sip@nap.demo");
  await expect(page.getByRole("region", { name: "Encaminhamentos" })).toBeVisible();
  const nav = page.getByRole("navigation", { name: "Principal" });
  await expect(nav.getByRole("link", { name: "Encaminhamentos" })).toBeVisible();
  await expect(nav.getByRole("link", { name: /Comunicações/ })).toBeVisible();
  await expect(nav.getByRole("link", { name: "Alunos" })).toHaveCount(0);
  await expect(nav.getByRole("link", { name: "Atividades" })).toHaveCount(0);
  await expect(nav.getByRole("link", { name: "Produção" })).toHaveCount(0);
});

for (const email of ["ana.beatriz@nap.demo", "coordenacao@nap.demo"]) {
  test(`Produção aparece no menu de ${email}`, async ({ page }) => {
    await entrar(page, email);
    await expect(page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Produção" })).toBeVisible();
  });
}

// Coordenação tem o mesmo número de botões: "Administração" fica no menu "Mais", fora da barra.
for (const [largura, email] of [320, 360].flatMap((l) => ["ana.beatriz@nap.demo", "coordenacao@nap.demo"].map((e) => [l, e] as const))) {
  test(`barra inferior de ${email} cabe em ${largura} px`, async ({ page }) => {
    await page.setViewportSize({ width: largura, height: 700 });
    await entrar(page, email);
    const nav = page.getByRole("navigation", { name: "Principal" });
    await expect(nav).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
    // Só os botões da barra: os links do menu "Mais" (popover) ficam fora.
    const itens = nav.locator(":scope > a, :scope > button[popovertarget]");
    expect(await itens.count()).toBe(6); // Painel, Alunos, Agenda, Atendimentos, Comunicações e Mais
    for (const item of await itens.all()) {
      const caixa = await item.boundingBox();
      expect(caixa && caixa.x >= 0 && caixa.x + caixa.width <= largura, await item.innerText()).toBe(true);
      // Rótulo sem corte: o texto não transborda o item.
      expect(await item.evaluate((el) => el.scrollWidth <= el.clientWidth), await item.innerText()).toBe(true);
    }
  });
}
