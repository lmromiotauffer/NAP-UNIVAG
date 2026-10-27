import { expect, test } from "@playwright/test";
import { hojeCivil, entrar, sair, somarDias, tokenDe } from "./apoio";

/** Segunda-feira da semana corrente; as datas da jornada saem dela, sem depender do dia da semana de hoje. */
function segundaAtual(): string {
  const hoje = hojeCivil(new Date());
  return somarDias(hoje, -((new Date(`${hoje}T12:00:00Z`).getUTCDay() + 6) % 7));
}

// Datas fixas em relação à segunda atual: a reunião cai na terça de daqui a duas semanas (o seed só agenda até a semana corrente, então
// Ana e Carlos estão livres) e o atendimento autorizado na terça da semana passada, às 18h (fora da escala padrão 7h–17h e fora dos
// horários do seed, 9h e 14h). A produção só conta tempo já decorrido, por isso o atendimento autorizado fica no passado. Rafael não
// é usado por outras jornadas: a semana dele nunca é fechada aqui.
test("reunião da equipe em grupo, cancelamento individual e atendimento autorizado fora da escala", async ({ page, request }) => {
  const sufixo = Date.now().toString(36).toUpperCase();
  const titulo = `Reunião da equipe ${sufixo}`;
  const justificativa = `Plantão de reposição ${sufixo}`;
  const segunda = segundaAtual();
  const tercaFutura = somarDias(segunda, 15);
  const segundaFutura = somarDias(segunda, 14);
  const segundaPassada = somarDias(segunda, -7);
  const tercaPassada = somarDias(segunda, -6);

  // Coordenação cria a reunião com Ana e Carlos.
  await entrar(page, "coordenacao@nap.demo");
  await page.goto(`/agenda?semana=${segundaFutura}`);
  await page.getByRole("button", { name: "Novo compromisso" }).click();
  const novo = page.getByRole("dialog", { name: "Novo compromisso" });
  await novo.getByLabel("Tipo").selectOption({ label: "Reunião de equipe" });
  await novo.getByLabel("Título").fill(titulo);
  await novo.getByLabel("Data").fill(tercaFutura);
  await novo.getByLabel("Início").fill("10:00");
  await novo.getByLabel("Fim").fill("11:00");
  await novo.getByRole("group", { name: "Participantes" }).getByLabel("Ana Beatriz Rocha").check();
  await novo.getByRole("group", { name: "Participantes" }).getByLabel("Carlos Mendes").check();
  await novo.getByRole("button", { name: "Agendar" }).click();
  await expect(novo).toBeHidden();
  await sair(page);

  // A reunião aparece na agenda dos dois; Carlos cancela só a participação dele.
  await entrar(page, "ana.beatriz@nap.demo");
  await page.goto(`/agenda?semana=${segundaFutura}`);
  await expect(page.getByRole("button", { name: new RegExp(titulo) })).toBeVisible();
  await sair(page);

  await entrar(page, "carlos.mendes@nap.demo");
  await page.goto(`/agenda?semana=${segundaFutura}`);
  await page.getByRole("button", { name: new RegExp(titulo) }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Cancelar minha participação" }).click();
  const cancelamento = page.getByRole("dialog", { name: "Cancelar minha participação" });
  await cancelamento.getByLabel("Motivo do cancelamento").fill(`Férias ${sufixo}`);
  await cancelamento.getByRole("button", { name: "Confirmar cancelamento" }).click();
  await expect(cancelamento).toBeHidden();
  await expect(page.getByRole("button", { name: new RegExp(titulo) })).toHaveCount(0);
  await sair(page);

  // Ana ainda vê a reunião.
  await entrar(page, "ana.beatriz@nap.demo");
  await page.goto(`/agenda?semana=${segundaFutura}`);
  await expect(page.getByRole("button", { name: new RegExp(titulo) })).toBeVisible();
  await sair(page);

  // Sem autorização, o atendimento das 18h do Rafael é recusado (fora da escala).
  const token = await tokenDe(request, "rafael.torres@nap.demo");
  const auth = { Authorization: `Bearer ${token}` };
  const alunos = await request.get("/api/alunos", { params: { busca: "DEMO-004" }, headers: auth });
  expect(alunos.ok()).toBe(true);
  const alunoId = ((await alunos.json()) as { itens: { id: string }[] }).itens[0]!.id;
  const registrar = (chave: string) =>
    request.post("/api/atendimentos", {
      headers: { ...auth, "Idempotency-Key": chave },
      data: {
        alunoId,
        inicio: `${tercaPassada}T18:00:00-04:00`,
        fim: `${tercaPassada}T18:50:00-04:00`,
        tipo: "Acompanhamento",
        estado: "Realizado",
        demandaPrincipal: `Plantão ${sufixo}`,
        justificativaAvulso: "Atendimento fora do horário, autorizado pela coordenação.",
      },
    });
  expect((await registrar(crypto.randomUUID())).status()).toBe(422);

  // Coordenação autoriza o horário na tela de Escalas.
  await entrar(page, "coordenacao@nap.demo");
  await page.goto("/administracao?aba=escalas");
  await page.getByRole("button", { name: "Nova autorização" }).click();
  const autorizacao = page.getByRole("dialog", { name: "Nova autorização" });
  await autorizacao.getByLabel("Profissional").selectOption({ label: "Rafael Torres" });
  await autorizacao.getByLabel("Início").fill(`${tercaPassada}T17:30`);
  await autorizacao.getByLabel("Fim").fill(`${tercaPassada}T19:30`);
  await autorizacao.getByLabel("Justificativa").fill(justificativa);
  await autorizacao.getByRole("button", { name: "Autorizar" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Autorização criada." })).toBeVisible();
  await expect(page.getByRole("table", { name: "Autorizações fora da escala" }).getByText(justificativa)).toBeVisible();

  // Agora o registro é aceito e a produção mostra 50 min de tempo extra.
  expect((await registrar(crypto.randomUUID())).status()).toBe(201);
  await page.goto(`/producao?tipo=Semana&referencia=${segundaPassada}`);
  await page.getByLabel("Profissional").selectOption({ label: "Rafael Torres" });
  const tempoExtra = page.getByText("Tempo extra", { exact: true }).locator("xpath=following-sibling::dd");
  await expect(tempoExtra).toHaveText("50 min");
});
