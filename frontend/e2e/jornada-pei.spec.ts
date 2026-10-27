import { expect, test } from "@playwright/test";
import { hojeCivil, entrar, sair, tokenDe } from "./apoio";

test("PEI percorre revisão, assinaturas, publicação e ciência da coordenação de curso", async ({ page, browser, request }) => {
  const sufixo = Date.now().toString(36).toUpperCase();
  const nome = `Aluno Pei ${sufixo}`;
  const matricula = `E2E-PEI-${sufixo}`;
  const adaptacoes = `Prova em sala separada ${sufixo}`;
  const demanda = `Demanda interna ${sufixo}`;
  const estado = (rotulo: string) => page.getByText(rotulo, { exact: true }).first();

  // Aluno do curso Direito, criado pela API com a Ana como responsável.
  const token = await tokenDe(request, "ana.beatriz@nap.demo");
  const auth = { Authorization: `Bearer ${token}` };
  const { id: anaId } = (await (await request.get("/api/auth/eu", { headers: auth })).json()) as { id: string };
  const cursos = (await (await request.get("/api/cursos", { headers: auth })).json()) as { id: string; nome: string }[];
  const cadastro = await request.post("/api/alunos", {
    headers: auth,
    data: {
      nomeCompleto: nome,
      matricula,
      cursoId: cursos.find((c) => c.nome === "Direito")?.id,
      semestre: 2,
      inicioAcompanhamento: hojeCivil(new Date()),
      profissionalResponsavelId: anaId,
    },
  });
  expect(cadastro.ok()).toBe(true);
  const alunoId = ((await cadastro.json()) as { id: string }).id;

  // Profissional: cria o PEI, preenche e envia para revisão.
  await entrar(page, "ana.beatriz@nap.demo");
  await page.goto(`/alunos/${alunoId}`);
  await page.getByRole("tab", { name: "PEI" }).click();
  await page.getByRole("button", { name: "Criar PEI" }).click();
  const criar = page.getByRole("dialog", { name: "Criar PEI" });
  await criar.getByLabel("Profissional responsável").selectOption(anaId);
  await criar.getByLabel("Início da vigência").fill(hojeCivil(new Date()));
  await criar.getByLabel("Fim da vigência").fill(hojeCivil(new Date(Date.now() + 180 * 86_400_000)));
  await criar.getByRole("button", { name: "Criar PEI", exact: true }).click();
  await expect(page).toHaveURL(/\/peis\/versoes\/[0-9a-f-]+$/);
  const urlVersao = page.url();

  await page.getByLabel("Adaptações").fill(adaptacoes);
  await page.getByLabel("Demanda interna").fill(demanda);
  await page.getByRole("button", { name: "Salvar rascunho" }).click();
  await expect(page.getByText("Rascunho salvo.")).toBeVisible();
  await page.getByRole("button", { name: "Enviar para revisão" }).click();
  await expect(estado("Em revisão")).toBeVisible();
  await sair(page);

  // Coordenação NAP aprova a revisão.
  await entrar(page, "coordenacao@nap.demo");
  await page.goto(urlVersao);
  await page.getByRole("button", { name: "Aprovar revisão" }).click();
  await expect(estado("Aguardando assinaturas")).toBeVisible();
  await sair(page);

  // Profissional assina e gera o acesso do aluno (link e código só aparecem na tela).
  await entrar(page, "ana.beatriz@nap.demo");
  await page.goto(urlVersao);
  await page.getByRole("button", { name: "Assinar como profissional" }).click();
  await expect(page.getByRole("button", { name: "Assinar como profissional" })).toHaveCount(0);
  await page.getByRole("button", { name: "Gerar acesso do aluno" }).click();
  const acesso = page.getByRole("dialog", { name: "Acesso do aluno para assinar" });
  await acesso.getByRole("button", { name: "Gerar link e código" }).click();
  const link = (await acesso.locator("code").innerText()).trim();
  const codigo = (await acesso.locator("strong").innerText()).trim();
  expect(link).toMatch(/\/assinar\/.+/);
  expect(codigo).toMatch(/^\d{6}$/);
  await acesso.getByRole("button", { name: "Fechar", exact: true }).last().click();

  // Aluno: contexto novo, sem sessão.
  const contextoAluno = await browser.newContext();
  try {
    const aluno = await contextoAluno.newPage();
    await aluno.goto(link);
    await aluno.getByLabel("Digite o código de 6 dígitos").fill(codigo);
    await aluno.getByRole("button", { name: "Continuar" }).click();
    await expect(aluno.getByRole("heading", { level: 1, name: `PEI de ${nome}` })).toBeVisible();
    await expect(aluno.getByText(adaptacoes)).toBeVisible();
    await expect(aluno.getByText(demanda)).toHaveCount(0);
    await aluno.getByLabel("Nome completo").fill(nome);
    await aluno.getByLabel(/Li e concordo/).check();
    await aluno.getByRole("button", { name: "Assinar", exact: true }).click();
    await expect(aluno.getByRole("heading", { name: /Assinatura registrada/ })).toBeVisible();
  } finally {
    await contextoAluno.close();
  }
  await page.reload();
  await expect(estado("Assinada")).toBeVisible();
  await sair(page);

  // Coordenação NAP publica.
  await entrar(page, "coordenacao@nap.demo");
  await page.goto(urlVersao);
  await page.getByRole("button", { name: "Publicar" }).click();
  await expect(estado("Publicada")).toBeVisible();
  await sair(page);

  // Coordenação de Direito: recebe o envio, abre a versão sem a demanda interna, baixa o PDF e registra ciência.
  await entrar(page, "coord.direito@nap.demo");
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: /Comunicações/ }).click();
  await expect(page.getByRole("link", { name: new RegExp(`PEI publicado: ${nome}`) })).toBeVisible();
  await page.goto(`/alunos/${alunoId}`);
  await page.getByRole("tab", { name: "PEI" }).click();
  await page.getByRole("link", { name: "Versão 1" }).click();
  await expect(page.getByRole("heading", { level: 1, name: `PEI de ${nome}` })).toBeVisible();
  await expect(page.getByText(adaptacoes)).toBeVisible();
  await expect(page.getByText("Demanda interna")).toHaveCount(0);
  await expect(page.getByText(demanda)).toHaveCount(0);
  const [download] = await Promise.all([page.waitForEvent("download"), page.getByRole("button", { name: "Baixar PDF" }).click()]);
  expect(download.suggestedFilename()).toBe(`PEI-${matricula.toUpperCase()}-v1.pdf`);
  await page.getByRole("button", { name: "Registrar ciência" }).click();
  await expect(page.getByText(/Ciência registrada em/)).toBeVisible();
  await sair(page);

  // Profissional: vê a ciência na página da versão.
  await entrar(page, "ana.beatriz@nap.demo");
  await page.goto(urlVersao);
  await expect(page.getByText(/Ciência registrada em/)).toBeVisible();
});
