import { expect, test } from "@playwright/test";
import { entrar, tokenDe } from "./apoio";
import { readFileSync } from "node:fs";

test("exportação da produção, 403 do profissional na auditoria e consulta da trilha", async ({ page, request }) => {
  await entrar(page, "coordenacao@nap.demo");

  // Produção: exporta PDF e Excel com o recorte da tela.
  await page.goto("/producao");
  for (const [botao, extensao, assinatura] of [["Exportar PDF", "pdf", "%PDF"], ["Exportar Excel", "xlsx", "PK"]] as const) {
    const [download] = await Promise.all([page.waitForEvent("download"), page.getByRole("button", { name: botao }).click()]);
    expect(download.suggestedFilename()).toMatch(new RegExp(String.raw`^Producao-.+-\d{4}-\d{2}-\d{2}\.${extensao}$`));
    const caminho = await download.path();
    // Assinatura do formato: PDF começa com "%PDF"; o XLSX é um ZIP ("PK").
    expect(readFileSync(caminho).subarray(0, assinatura.length).toString("latin1")).toBe(assinatura);
  }

  // Profissional não consulta a auditoria: 403 pela API (gera o evento negado).
  const token = await tokenDe(request, "carlos.mendes@nap.demo");
  // Só o status importa: qualquer período válido serve.
  const negado = await request.get("/api/auditoria", { params: { inicio: "2026-01-05", fim: "2026-01-09" }, headers: { Authorization: `Bearer ${token}` } });
  expect(negado.status()).toBe(403);

  // Administração → Auditoria: as duas exportações.
  await page.goto("/administracao?aba=auditoria");
  const eventos = page.getByRole("table", { name: "Trilha de auditoria" });
  await page.getByLabel("Ação", { exact: true }).fill("producao.exportar");
  await page.getByRole("button", { name: "Filtrar" }).click();
  await expect(eventos.getByRole("cell", { name: "producao.exportar.pdf", exact: true }).first()).toBeVisible();
  await expect(eventos.getByRole("cell", { name: "producao.exportar.xlsx", exact: true }).first()).toBeVisible();

  // Resultado "negado" e recurso da consulta: a tentativa do profissional.
  await page.getByLabel("Ação", { exact: true }).fill("");
  await page.getByLabel("Recurso", { exact: true }).fill("GET api/auditoria");
  await page.getByLabel(/^Resultado/).selectOption("negado");
  await page.getByRole("button", { name: "Filtrar" }).click();
  const tentativa = eventos.getByRole("row").filter({ hasText: "acesso.negado" }).filter({ hasText: "Carlos Mendes" }).first();
  await expect(tentativa).toBeVisible();
  await expect(tentativa.getByText("negado", { exact: true })).toBeVisible();
});
