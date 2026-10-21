import { expect, test } from "@playwright/test";
import { entrar, sair, tokenDe } from "./apoio";

const diaCuiaba = (d: Date) => new Intl.DateTimeFormat("en-CA", { timeZone: "America/Cuiaba" }).format(d);
const somarDias = (dia: string, n: number) => diaCuiaba(new Date(new Date(`${dia}T12:00:00Z`).getTime() + n * 86_400_000));

/** Segunda-feira da semana passada (a semana só fecha depois de encerrada). */
function segundaPassada(): string {
  const hoje = diaCuiaba(new Date());
  const diaDaSemana = new Date(`${hoje}T12:00:00Z`).getUTCDay();
  return somarDias(hoje, -((diaDaSemana + 6) % 7) - 7);
}

// O banco NapE2E é recriado a cada execução. As outras jornadas só mexem na Ana no dia útil anterior, que pode cair
// na semana passada: aqui o profissional é o Carlos (quarta-feira da semana passada; o seed ocupa 9h e 14h dele).
test("realização, fechamento, reabertura, ajuste e leitura por perfil", async ({ page, request }) => {
  const sufixo = Date.now().toString(36).toUpperCase();
  const titulo = `Reunião de alinhamento ${sufixo}`;
  const justificativaReabertura = `Falta lançar um atendimento ${sufixo}`;
  const justificativaAjuste = `Atendimentos fora do sistema ${sufixo}`;
  const segunda = segundaPassada();
  const quarta = somarDias(segunda, 2);
  const semanaPassada = `/producao?tipo=Semana&referencia=${segunda}`;

  // Preparação pela API: um atendimento realizado (10:30) e uma reunião de equipe ainda sem desfecho (15:30).
  const token = await tokenDe(request, "carlos.mendes@nap.demo");
  const auth = { Authorization: `Bearer ${token}` };
  const alunos = await request.get("/api/alunos", { params: { busca: "DEMO-002" }, headers: auth });
  expect(alunos.ok()).toBe(true);
  const alunoId = ((await alunos.json()) as { itens: { id: string }[] }).itens[0]!.id;
  const atendimento = await request.post("/api/atendimentos", {
    headers: { ...auth, "Idempotency-Key": crypto.randomUUID() },
    data: {
      alunoId,
      inicio: `${quarta}T10:30:00-04:00`,
      fim: `${quarta}T11:20:00-04:00`,
      tipo: "Acompanhamento",
      estado: "Realizado",
      demandaPrincipal: `Rotina de estudos ${sufixo}`,
      justificativaAvulso: "Atendimento lançado depois do horário.",
    },
  });
  expect(atendimento.status()).toBe(201);
  const atendimentoId = ((await atendimento.json()) as { id: string }).id;
  const reuniao = await request.post("/api/agenda", {
    headers: { ...auth, "Idempotency-Key": crypto.randomUUID() },
    data: { tipo: "ReuniaoEquipe", titulo, inicio: `${quarta}T15:30:00-04:00`, fim: `${quarta}T16:30:00-04:00` },
  });
  expect(reuniao.status()).toBe(201);

  // Profissional registra a realização da reunião (horário efetivo padrão = agendado).
  await entrar(page, "carlos.mendes@nap.demo");
  await page.goto(`/agenda?semana=${segunda}`);
  await page.getByRole("button", { name: new RegExp(titulo) }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Registrar realização" }).click();
  const realizacao = page.getByRole("dialog", { name: "Registrar realização" });
  await realizacao.getByRole("button", { name: "Registrar realização" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Realização registrada." })).toBeVisible();

  // Produção: Semana, volta um período, e a reunião e o atendimento aparecem.
  await page.goto("/producao");
  await page.getByLabel("Tipo de período").selectOption({ label: "Semana" });
  await page.getByRole("button", { name: "Período anterior" }).click();
  const formato = (d: string) => d.split("-").reverse().join("/");
  await expect(page.getByRole("group", { name: "Período" }).getByText(`${formato(segunda)} a ${formato(somarDias(segunda, 6))}`)).toBeVisible();
  const categorias = page.getByRole("table", { name: "Produção por categoria" });
  await expect(categorias.getByRole("row", { name: /Reuniões de equipe/ }).getByRole("cell").first()).toHaveText("1");
  await expect(categorias.getByRole("row", { name: /Atendimentos realizados/ }).getByRole("cell").first()).toHaveText(/^[1-9]\d*$/);

  // Fecha a semana (confirma se houver pendências do seed).
  await page.getByRole("tab", { name: "Semanas" }).click();
  await page.getByRole("button", { name: /^Fechar semana de .*Carlos Mendes$/ }).click();
  await page.getByRole("dialog", { name: "Fechar semana" }).getByRole("button", { name: "Fechar semana" }).click();
  await expect(page.getByRole("button", { name: /^Fechar semana de/ })).toHaveCount(0);
  await expect(page.getByRole("table", { name: "Semanas do período" }).getByText("Fechada", { exact: true })).toBeVisible();

  // Corrigir o atendimento da semana fechada é recusado.
  await page.goto(`/atendimentos?id=${atendimentoId}`);
  await page.getByRole("dialog", { name: "Atendimento" }).getByRole("button", { name: "Corrigir" }).click();
  const correcao = page.getByRole("dialog", { name: "Corrigir atendimento" });
  await correcao.getByLabel("Tipo").selectOption("Devolutiva");
  await correcao.getByRole("button", { name: "Salvar correção" }).click();
  await expect(correcao.getByText("A semana está fechada; peça a reabertura à coordenação.")).toBeVisible();
  // O <dialog> modal deixa o resto da página inerte: fecha antes de sair.
  await correcao.getByRole("button", { name: "Fechar" }).click();
  await expect(correcao).toBeHidden();
  await sair(page);

  // Coordenação reabre com justificativa e lança um ajuste.
  await entrar(page, "coordenacao@nap.demo");
  await page.goto(`${semanaPassada}&aba=semanas`);
  await page.getByRole("button", { name: /^Reabrir semana de .*Carlos Mendes$/ }).click();
  const reabertura = page.getByRole("dialog", { name: "Reabrir semana" });
  await reabertura.getByLabel("Justificativa").fill(justificativaReabertura);
  await reabertura.getByRole("button", { name: "Reabrir semana" }).click();
  await expect(page.getByText(justificativaReabertura)).toBeVisible();

  await page.getByRole("tab", { name: "Ajustes" }).click();
  await page.getByRole("button", { name: "Novo ajuste" }).click();
  const ajuste = page.getByRole("dialog", { name: "Novo ajuste" });
  await ajuste.getByLabel("Profissional").selectOption({ label: "Carlos Mendes" });
  await ajuste.getByLabel("Data").fill(quarta);
  await ajuste.getByLabel("Categoria").selectOption({ label: "Atendimentos realizados" });
  await ajuste.getByLabel("Ocorrências").fill("2");
  await ajuste.getByLabel("Minutos").fill("100");
  await ajuste.getByLabel("Justificativa").fill(justificativaAjuste);
  await ajuste.getByRole("button", { name: "Registrar ajuste" }).click();
  await expect(page.getByRole("table", { name: "Ajustes do período" }).getByText(justificativaAjuste)).toBeVisible();
  await sair(page);

  // Coordenação de curso: só indicadores do curso, sem capacidade nem comparação.
  await entrar(page, "coord.direito@nap.demo");
  await page.goto(semanaPassada);
  await expect(page.getByRole("table", { name: "Produção por categoria" }).locator("tbody tr")).toHaveCount(3);
  await expect(page.getByRole("heading", { name: "Capacidade e tempo" })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Comparação entre profissionais" })).toHaveCount(0);
  await expect(page.getByRole("tab", { name: "Semanas" })).toHaveCount(0);
  await sair(page);

  // Gestão: comparação entre profissionais.
  await entrar(page, "gestao@nap.demo");
  await page.goto(semanaPassada);
  const comparacao = page.getByRole("table", { name: "Comparação entre profissionais" });
  await expect(comparacao.getByRole("rowheader", { name: "Carlos Mendes" })).toBeVisible();
  await expect(comparacao.getByRole("rowheader", { name: "Ana Beatriz Rocha" })).toBeVisible();
});
