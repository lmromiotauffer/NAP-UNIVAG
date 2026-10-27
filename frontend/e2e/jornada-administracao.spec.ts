import { expect, test } from "@playwright/test";
import { hojeCivil, entrar, tokenDe } from "./apoio";

/** Próxima data (estritamente depois de hoje, no fuso do NAP) que cai no dia da semana pedido (0 = domingo). */
function proximoDia(diaSemana: number): string {
  const hoje = new Date(`${hojeCivil(new Date())}T12:00:00Z`);
  const falta = ((diaSemana - hoje.getUTCDay() + 7) % 7) || 7;
  hoje.setUTCDate(hoje.getUTCDate() + falta);
  return hoje.toISOString().slice(0, 10);
}

test("coordenação cria profissional, escala e feriado; a agenda recusa; inativar encerra a sessão", async ({ page, browser, baseURL, request }) => {
  const sufixo = Date.now().toString(36);
  const nome = `Profissional Jornada ${sufixo}`;
  const email = `prof.jornada.${sufixo}@nap.demo`;
  const novaSenha = `Definida${sufixo}9x`;
  const segunda = proximoDia(1);
  const quarta = proximoDia(3);
  const descricaoFeriado = `Feriado jornada ${sufixo}`;

  // Coordenação: cria o profissional e lê a senha temporária (exibida uma única vez).
  await entrar(page, "coordenacao@nap.demo");
  await page.goto("/administracao");
  await page.getByRole("button", { name: "Novo usuário" }).click();
  const novo = page.getByRole("dialog", { name: "Novo usuário" });
  await novo.getByLabel("Nome completo").fill(nome);
  await novo.getByLabel("E-mail").fill(email);
  await novo.getByLabel("Perfil").selectOption("ProfissionalNap");
  await novo.getByRole("button", { name: "Salvar" }).click();
  const entrega = page.getByRole("dialog", { name: "Senha temporária" });
  await expect(entrega.getByText(email)).toBeVisible();
  const temporaria = (await entrega.locator("code").innerText()).trim();
  expect(temporaria).toHaveLength(16);
  await entrega.getByRole("button", { name: "Concluir" }).click();
  await expect(entrega).toHaveCount(0);

  // Novo usuário, em outro contexto: é levado a "Defina uma nova senha", troca e chega ao painel.
  const contextoProf = await browser.newContext({ baseURL, locale: "pt-BR", timezoneId: "America/Cuiaba" });
  const prof = await contextoProf.newPage();
  try {
    await entrar(prof, email, temporaria);
    await expect(prof).toHaveURL(/\/definir-senha$/);
    await expect(prof.getByRole("heading", { level: 1, name: "Defina uma nova senha" })).toBeVisible();
    await prof.getByLabel("Senha atual").fill(temporaria);
    await prof.getByLabel("Nova senha", { exact: true }).fill(novaSenha);
    await prof.getByLabel("Confirmar nova senha").fill(novaSenha);
    await prof.getByRole("button", { name: "Salvar nova senha" }).click();
    await expect(prof.getByRole("navigation", { name: "Principal" })).toBeVisible();
    await expect(prof).toHaveURL(/\/$/);

    // Coordenação: escala do novo profissional, sem atendimento às quartas.
    await page.goto("/administracao?aba=escalas");
    await page.getByRole("button", { name: "Nova escala" }).click();
    const escala = page.getByRole("dialog", { name: "Nova escala" });
    await escala.getByLabel("Profissional").selectOption({ label: nome });
    await escala.getByLabel("Início da vigência").fill(hojeCivil(new Date()));
    await escala.getByLabel("Quarta-feira", { exact: true }).uncheck();
    await escala.getByRole("button", { name: "Salvar" }).click();
    await expect(page.getByRole("status").filter({ hasText: "Escala cadastrada." })).toBeVisible();

    // Coordenação: feriado na próxima segunda. Se a agenda já tem algo nessa data, os conflitos aparecem.
    await page.getByRole("tab", { name: "Feriados" }).click();
    await page.getByRole("button", { name: "Novo feriado" }).click();
    const feriado = page.getByRole("dialog", { name: "Novo feriado" });
    await feriado.getByLabel("Data").fill(segunda);
    await feriado.getByLabel("Descrição").fill(descricaoFeriado);
    await feriado.getByRole("button", { name: "Salvar" }).click();
    const conflitos = page.getByRole("dialog", { name: "Feriado cadastrado com conflitos" });
    const cadastrado = page.getByRole("status").filter({ hasText: "Feriado cadastrado." });
    await expect(conflitos.or(cadastrado)).toBeVisible();
    if (await conflitos.isVisible()) {
      await expect(conflitos.getByText("Nada foi apagado. Resolva cada compromisso na agenda.")).toBeVisible();
      await conflitos.getByRole("button", { name: "Fechar" }).first().click();
    }
    // A lista abre no ano corrente; a próxima segunda pode cair no seguinte.
    await page.getByLabel("Ano").selectOption(segunda.slice(0, 4));
    await expect(page.getByRole("row", { name: new RegExp(descricaoFeriado) })).toBeVisible();

    // Profissional: tenta agendar na quarta (fora da escala) e na segunda (feriado).
    async function tentarAgendar(dia: string, mensagem: string) {
      await prof.goto("/agenda");
      await prof.getByRole("button", { name: "Novo compromisso" }).first().click();
      const form = prof.getByRole("dialog", { name: "Novo compromisso" });
      await form.getByLabel("Tipo").selectOption("ReuniaoEquipe");
      await form.getByLabel("Título").fill("Reunião de teste");
      await form.getByLabel("Data").fill(dia);
      await form.getByLabel("Início").fill("09:00");
      await form.getByLabel("Fim").fill("09:50");
      await form.getByRole("button", { name: "Agendar" }).click();
      await expect(form.getByText(mensagem)).toBeVisible();
    }
    await tentarAgendar(quarta, "Fora da escala do profissional neste dia.");
    await tentarAgendar(segunda, `Esta data é feriado: ${descricaoFeriado}.`);

    // Coordenação inativa o usuário: a próxima ação dele leva ao login.
    await page.goto("/administracao");
    await page.getByRole("searchbox", { name: "Buscar por nome ou e-mail" }).fill(email);
    await page.getByRole("button", { name: `Inativar ${nome}` }).click();
    await page.getByRole("dialog", { name: "Inativar usuário" }).getByRole("button", { name: "Inativar" }).click();
    await expect(page.getByRole("status").filter({ hasText: "Usuário inativado." })).toBeVisible();

    await prof.goto("/agenda");
    await expect(prof).toHaveURL(/\/entrar/);
  } finally {
    await contextoProf.close();
    // Limpeza pela API, tolerante a falha: o feriado não deve acompanhar o banco reaproveitado, mesmo se o teste quebrar no meio.
    try {
      const auth = { Authorization: `Bearer ${await tokenDe(request, "coordenacao@nap.demo")}` };
      const lista = await request.get(`/api/feriados?ano=${segunda.slice(0, 4)}`, { headers: auth });
      const itens = (await lista.json()) as { id: string; descricao: string }[];
      for (const f of itens.filter((i) => i.descricao === descricaoFeriado)) await request.post(`/api/feriados/${f.id}/inativar`, { headers: auth });
    } catch {
      // melhor esforço
    }
  }
});
