import { expect, test, type Page } from "@playwright/test";
import { entrar, sair, tokenDe } from "./apoio";

const diaCuiaba = (d: Date) => new Intl.DateTimeFormat("en-CA", { timeZone: "America/Cuiaba" }).format(d);

/** O selo só atualiza a cada 60 s: recarrega até o processador da outbox ter gerado o aviso. Qualquer total serve: o selo acumula não lidas deixadas pelas jornadas anteriores (em série) para os mesmos usuários de demonstração. */
async function aguardarSeloDeNotificacao(page: Page) {
  const link = page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: /[1-9]\d* não lidas/ });
  await expect(async () => {
    await page.reload();
    await expect(link).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 15_000 });
}

test("profissional encaminha ao SIP, o SIP acolhe e a coordenação de curso não vê o motivo", async ({ page, request }) => {
  const sufixo = Date.now().toString(36).toUpperCase();
  const nome = `Aluno Encaminhado ${sufixo}`;
  const motivo = `Motivo sigiloso ${sufixo}`;

  // Aluno do curso Direito, criado pela API com a Ana como responsável.
  const token = await tokenDe(request, "ana.beatriz@nap.demo");
  const auth = { Authorization: `Bearer ${token}` };
  const { id: anaId } = (await (await request.get("/api/auth/eu", { headers: auth })).json()) as { id: string };
  const cursos = (await (await request.get("/api/cursos", { headers: auth })).json()) as { id: string; nome: string }[];
  const cadastro = await request.post("/api/alunos", {
    headers: auth,
    data: {
      nomeCompleto: nome,
      matricula: `E2E-ENC-${sufixo}`,
      cursoId: cursos.find((c) => c.nome === "Direito")?.id,
      semestre: 2,
      inicioAcompanhamento: diaCuiaba(new Date()),
      profissionalResponsavelId: anaId,
    },
  });
  expect(cadastro.ok()).toBe(true);
  const alunoId = ((await cadastro.json()) as { id: string }).id;

  // Profissional: abre a ficha e encaminha Psicologia → SIP.
  await entrar(page, "ana.beatriz@nap.demo");
  await page.goto(`/alunos/${alunoId}`);
  await expect(page.getByRole("heading", { level: 1, name: nome })).toBeVisible();
  await page.getByRole("tab", { name: "Encaminhamentos" }).click();
  await page.getByRole("button", { name: "Novo encaminhamento" }).click();
  const novo = page.getByRole("dialog", { name: "Novo encaminhamento" });
  await novo.getByLabel("Especialidade").selectOption({ label: "Psicologia" });
  await novo.getByLabel("Destino").selectOption({ label: "SIP" });
  await novo.getByLabel("Motivo restrito").fill(motivo);
  await novo.getByLabel("Resumo compartilhável").fill("Acompanhamento psicológico sugerido.");
  await novo.getByRole("button", { name: "Encaminhar" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Encaminhamento registrado." })).toBeVisible();
  await sair(page);

  // SIP: vê o selo, abre a notificação, chega ao detalhe e registra o acolhimento.
  await entrar(page, "sip@nap.demo");
  await aguardarSeloDeNotificacao(page);
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: /Comunicações/ }).click();
  await page.getByRole("tab", { name: "Notificações" }).click();
  await page.getByRole("listitem").filter({ hasText: nome }).getByRole("button", { name: /^Abrir/ }).click();
  await expect(page).toHaveURL(/\/encaminhamentos\/[0-9a-f-]+$/);
  await expect(page.getByRole("heading", { level: 1, name: nome })).toBeVisible();
  await expect(page.getByText(motivo)).toBeVisible(); // premissa: o SIP vê o motivo
  await page.getByRole("button", { name: "Registrar acolhimento" }).click();
  const retorno = page.getByRole("dialog", { name: "Registrar acolhimento" });
  await expect(retorno.getByLabel("Data do acolhimento")).toHaveValue(diaCuiaba(new Date()));
  await retorno.getByRole("button", { name: "Registrar" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Retorno registrado." })).toBeVisible();
  await sair(page);

  // Profissional: recebe o aviso do retorno e confere o estado.
  await entrar(page, "ana.beatriz@nap.demo");
  await aguardarSeloDeNotificacao(page);
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: /Comunicações/ }).click();
  await page.getByRole("tab", { name: "Notificações" }).click();
  await page.getByRole("listitem").filter({ hasText: nome }).getByRole("button", { name: /^Abrir/ }).click();
  await expect(page.getByRole("heading", { level: 1, name: nome })).toBeVisible();
  await expect(page.getByText("Acolhido").first()).toBeVisible();
  await sair(page);

  // Coordenação de Direito: vê o encaminhamento na ficha, mas não o texto do motivo.
  await entrar(page, "coord.direito@nap.demo");
  await page.goto(`/alunos/${alunoId}`);
  await expect(page.getByRole("heading", { level: 1, name: nome })).toBeVisible();
  await page.getByRole("tab", { name: "Encaminhamentos" }).click();
  const painel = page.getByRole("tabpanel");
  await expect(painel.getByText("Acolhido").first()).toBeVisible();
  await expect(painel.getByText(motivo)).toHaveCount(0);
  await painel.getByRole("link").first().click();
  await expect(page.getByRole("heading", { level: 1, name: nome })).toBeVisible();
  await expect(page.getByText(motivo)).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Motivo restrito" })).toHaveCount(0);
});
