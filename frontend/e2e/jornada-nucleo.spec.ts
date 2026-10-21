import { expect, test } from "@playwright/test";
import { entrar, sair } from "./apoio";

const diaCuiaba = (d: Date) => new Intl.DateTimeFormat("en-CA", { timeZone: "America/Cuiaba" }).format(d);

/** Dia útil mais recente antes de hoje: o horário já terminou e a agenda recusa fim de semana. */
function diaUtilAnterior(): string {
  let d = new Date();
  do d = new Date(d.getTime() - 86_400_000);
  while ([0, 6].includes(new Date(`${diaCuiaba(d)}T12:00:00Z`).getUTCDay()));
  return diaCuiaba(d);
}

test("cadastrar, agendar, registrar e reencontrar o aluno", async ({ page }) => {
  const sufixo = Date.now().toString(36).toUpperCase();
  const nome = `Aluno Jornada ${sufixo}`;
  const matricula = `E2E-${sufixo}`;
  // O seed ocupa 09:00, 11:00 e 14:00 (seg–qui): 12:00 fica livre para a Ana.
  const dia = diaUtilAnterior();

  await entrar(page, "ana.beatriz@nap.demo");

  // Cadastro
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Alunos" }).click();
  await page.getByRole("button", { name: "Cadastrar aluno" }).first().click();
  const cadastro = page.getByRole("dialog", { name: "Cadastrar aluno" });
  await cadastro.getByLabel("Nome completo").fill(nome);
  await cadastro.getByLabel("Matrícula").fill(matricula);
  await cadastro.getByLabel("Curso").selectOption({ label: "Psicologia" });
  await cadastro.getByLabel("Semestre").fill("3");
  await cadastro.getByLabel("Início do acompanhamento").fill(diaCuiaba(new Date()));
  await cadastro.getByLabel("Profissional responsável").selectOption({ label: "Ana Beatriz Rocha" });
  await cadastro.getByRole("button", { name: "Salvar" }).click();
  await expect(page.getByRole("heading", { level: 1, name: nome })).toBeVisible();
  const urlFicha = page.url();

  // Agendamento pela ficha
  await page.getByRole("button", { name: "Agendar" }).click();
  const novo = page.getByRole("dialog", { name: "Novo compromisso" });
  await expect(novo.getByText(nome)).toBeVisible();
  await novo.getByLabel("Data").fill(dia);
  await novo.getByLabel("Início").fill("12:00");
  await novo.getByLabel("Fim").fill("12:50");
  await novo.getByRole("button", { name: "Agendar" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Compromisso agendado." })).toBeVisible();

  // Registro de "Realizado" pelo detalhe do compromisso na agenda
  await page.getByRole("button", { name: new RegExp(`^${nome}, 12h00–12h50, Atendimento`) }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Registrar atendimento" }).click();
  const registro = page.getByRole("dialog", { name: "Registrar atendimento" });
  await expect(registro.getByLabel("Realizado")).toBeChecked();
  await registro.getByLabel("Tipo").selectOption({ index: 1 });
  await registro.getByLabel("Demanda principal").fill("Organização da rotina de estudos.");
  await registro.getByRole("button", { name: "Registrar" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Atendimento registrado." })).toBeVisible();

  // Compromisso sobreposto: a API recusa
  await page.getByRole("button", { name: "Novo compromisso" }).click();
  const sobreposto = page.getByRole("dialog", { name: "Novo compromisso" });
  await sobreposto.getByLabel("Título").fill("Atendimento sobreposto");
  await sobreposto.getByLabel("Data").fill(dia);
  await sobreposto.getByLabel("Início").fill("12:30");
  await sobreposto.getByLabel("Fim").fill("13:30");
  await sobreposto.getByRole("searchbox", { name: "Aluno" }).fill(matricula);
  await sobreposto.getByRole("button", { name: new RegExp(nome) }).click();
  await sobreposto.getByRole("button", { name: "Agendar" }).click();
  await expect(sobreposto.getByText(/Conflito de horário/)).toBeVisible();
  await sobreposto.getByRole("button", { name: "Cancelar" }).click();

  // Ficha: 1 atendimento e linha do tempo
  await page.goto(urlFicha);
  const cartao = page.getByText("histórico completo").locator("..");
  await expect(cartao).toHaveText(/^Atendimentos\s*1\s*histórico completo$/);
  await page.getByRole("tab", { name: "Linha do tempo" }).click();
  const linha = page.getByRole("region", { name: "Linha do tempo" });
  await expect(linha.getByText(/^Atendimento: /)).toBeVisible();
  await expect(linha.getByRole("listitem")).not.toHaveCount(0);

  // Edição do aluno pela ficha (envia If-Match com a versão carregada)
  const nomeEditado = `${nome} Editado`;
  await page.getByRole("button", { name: "Editar dados" }).click();
  const edicao = page.getByRole("dialog", { name: "Editar dados do aluno" });
  await edicao.getByLabel("Nome completo").fill(nomeEditado);
  await edicao.getByRole("button", { name: "Salvar" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Dados do aluno atualizados." })).toBeVisible();
  await expect(page.getByRole("heading", { level: 1, name: nomeEditado })).toBeVisible();

  // Persistência: sair, entrar de novo e encontrar o aluno
  await sair(page);
  await entrar(page, "ana.beatriz@nap.demo");
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Alunos" }).click();
  await page.getByRole("searchbox", { name: "Buscar por nome ou matrícula" }).fill(matricula);
  await expect(page.getByRole("link", { name: `Abrir ficha de ${nomeEditado}` })).toBeVisible();
});
