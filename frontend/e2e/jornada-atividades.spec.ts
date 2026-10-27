import { expect, test } from "@playwright/test";
import { hojeCivil, entrar, sair } from "./apoio";

/** Dia útil mais recente antes de hoje: o horário já terminou e a agenda recusa fim de semana. */
function diaUtilAnterior(): string {
  let d = new Date();
  do d = new Date(d.getTime() - 86_400_000);
  while ([0, 6].includes(new Date(`${hojeCivil(d)}T12:00:00Z`).getUTCDay()));
  return hojeCivil(d);
}

// O banco NapE2E é recriado a cada execução e atividades não se apagam: os horários só precisam não colidir com
// as outras jornadas da mesma execução (a Ana usa 12:00 em jornada-nucleo; o seed ocupa 09:00, 11:00 e 14:00).
test("registrar a partir da agenda, registrar avulsa, corrigir e consultar por perfil", async ({ page }) => {
  const sufixo = Date.now().toString(36).toUpperCase();
  const titulo = `Roda de conversa ${sufixo}`;
  const descricaoCurso = `Acolhimento dos calouros ${sufixo}`;
  const descricaoAvulsa = `Reunião institucional ${sufixo}`;
  const dia = diaUtilAnterior();
  const lista = `/atividades?inicio=${dia}&fim=${dia}`;

  // Profissional: agenda "Atividade no curso" num horário já encerrado.
  await entrar(page, "ana.beatriz@nap.demo");
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Agenda" }).click();
  await page.getByRole("button", { name: "Novo compromisso" }).click();
  const novo = page.getByRole("dialog", { name: "Novo compromisso" });
  await novo.getByLabel("Tipo").selectOption({ label: "Atividade no curso" });
  await novo.getByLabel("Título").fill(titulo);
  await novo.getByLabel("Data").fill(dia);
  await novo.getByLabel("Início").fill("15:30");
  await novo.getByLabel("Fim").fill("16:20");
  await novo.getByRole("button", { name: "Agendar" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Compromisso agendado." })).toBeVisible();

  // Registro a partir do compromisso (horário vem preenchido); o compromisso fica concluído.
  const bloco = page.getByRole("button", { name: new RegExp(`^${titulo}, 15h30–16h20, Atividade no curso`) });
  await bloco.click();
  await page.getByRole("dialog").getByRole("button", { name: "Registrar atividade" }).click();
  const registro = page.getByRole("dialog", { name: "Registrar atividade" });
  await expect(registro.getByLabel("Início")).toHaveValue("15:30");
  await expect(registro.getByLabel("Fim")).toHaveValue("16:20");
  await registro.getByLabel("Curso").selectOption({ label: "Direito" });
  await registro.getByLabel("Tipo").selectOption({ label: "Roda de conversa" });
  await registro.getByLabel("Descrição").fill(descricaoCurso);
  await registro.getByRole("button", { name: "Registrar" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Atividade registrada." })).toBeVisible();
  await bloco.click();
  await expect(page.getByRole("dialog").getByRole("button", { name: "Cancelar compromisso" })).toHaveCount(0);
  await expect(page.getByRole("dialog").getByRole("button", { name: "Registrar atividade" })).toHaveCount(0);
  await page.keyboard.press("Escape");

  // Avulsa institucional às 19:00 (fora da escala), com justificativa.
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: "Atividades" }).click();
  await page.getByRole("button", { name: "Nova atividade" }).first().click();
  const avulsa = page.getByRole("dialog", { name: "Nova atividade avulsa" });
  await avulsa.getByLabel("Data").fill(dia);
  await avulsa.getByLabel("Início").fill("19:00");
  await avulsa.getByLabel("Fim").fill("20:00");
  await avulsa.getByLabel("Tipo").selectOption({ label: "Reunião pedagógica" });
  await avulsa.getByLabel("Descrição").fill(descricaoAvulsa);
  await avulsa.getByLabel("Justificativa da atividade avulsa").fill("Reunião noturna combinada com a coordenação.");
  await avulsa.getByRole("button", { name: "Registrar" }).click();
  // O aviso "Atividade registrada." do registro anterior ainda pode estar na tela (dura 5 s): o diálogo só fecha com o POST concluído.
  await expect(avulsa).toBeHidden();
  await expect(page.getByRole("status").filter({ hasText: "Atividade registrada." }).last()).toBeVisible();
  await page.goto(lista);
  await expect(page.getByRole("button", { name: /^Abrir atividade de .*: Reunião pedagógica, institucional$/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /^Abrir atividade de .*: Roda de conversa, Direito$/ })).toBeVisible();
  await sair(page);

  // Coordenação do NAP corrige com justificativa e vê o histórico.
  await entrar(page, "coordenacao@nap.demo");
  await page.goto(lista);
  await page.getByRole("button", { name: /^Abrir atividade de .*: Roda de conversa, Direito$/ }).click();
  const detalhe = page.getByRole("dialog", { name: "Atividade", exact: true });
  await expect(detalhe.getByText(descricaoCurso)).toBeVisible();
  await detalhe.getByRole("button", { name: "Corrigir" }).click();
  const correcao = page.getByRole("dialog", { name: "Corrigir atividade" });
  await correcao.getByLabel("Tipo").selectOption({ label: "Orientação coletiva" });
  await correcao.getByLabel("Justificativa da correção").fill("Tipo registrado errado.");
  await correcao.getByRole("button", { name: "Salvar correção" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Atividade corrigida." })).toBeVisible();
  await expect(detalhe.getByText("Correções (1)")).toBeVisible();
  await expect(detalhe.getByText("Justificativa: Tipo registrado errado.")).toBeVisible();
  await expect(detalhe.getByText(/Antes: Roda de conversa/)).toBeVisible();
  // O <dialog> modal deixa o resto da página inerte: fecha antes de sair.
  await detalhe.getByRole("button", { name: "Fechar" }).click();
  await expect(detalhe).toBeHidden();
  await sair(page);

  // Coordenação do curso: vê a do curso (só leitura) e não vê a institucional.
  await entrar(page, "coord.direito@nap.demo");
  await page.goto(lista);
  await page.getByRole("button", { name: /^Abrir atividade de .*: Orientação coletiva, Direito$/ }).click();
  const leitura = page.getByRole("dialog", { name: "Atividade", exact: true });
  await expect(leitura.getByText(descricaoCurso, { exact: true })).toBeVisible();
  await expect(leitura.getByRole("button", { name: "Corrigir" })).toHaveCount(0);
  await page.keyboard.press("Escape");
  await expect(page.getByRole("button", { name: /institucional$/ })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Nova atividade" })).toHaveCount(0);
});
