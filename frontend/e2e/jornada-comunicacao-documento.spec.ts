import { expect, test, type Page } from "@playwright/test";
import { hojeCivil, entrar, sair, tokenDe } from "./apoio";

// PDF mínimo: o que a API valida é a assinatura "%PDF-".
const PDF = Buffer.from("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");

/** O selo de não lidas só atualiza a cada 60 s: recarrega até haver algo não lido (mensagem e notificação somam no mesmo selo). */
async function aguardarSelo(page: Page) {
  const link = page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: /[1-9]\d* não lidas/ });
  await expect(async () => {
    await page.reload();
    await expect(link).toBeVisible({ timeout: 2_000 });
  }).toPass({ timeout: 15_000 });
}

test("profissional compartilha um documento e escreve à coordenação, que responde e baixa o arquivo", async ({ page, request }) => {
  const sufixo = Date.now().toString(36).toUpperCase();
  const nome = `Aluno Comunicado ${sufixo}`;
  const descricaoRestrita = `Laudo restrito ${sufixo}`;
  const descricaoCompartilhada = `Relatório devolutiva ${sufixo}`;
  const nomeArquivo = `relatorio-${sufixo}.pdf`;
  const assunto = `Devolutiva ${sufixo}`;
  const resposta = `Recebido, obrigado ${sufixo}`;

  // Aluno do curso Direito, criado pela API com a Ana como responsável.
  const token = await tokenDe(request, "ana.beatriz@nap.demo");
  const auth = { Authorization: `Bearer ${token}` };
  const { id: anaId } = (await (await request.get("/api/auth/eu", { headers: auth })).json()) as { id: string };
  const cursos = (await (await request.get("/api/cursos", { headers: auth })).json()) as { id: string; nome: string }[];
  const cadastro = await request.post("/api/alunos", {
    headers: auth,
    data: {
      nomeCompleto: nome,
      matricula: `E2E-COM-${sufixo}`,
      cursoId: cursos.find((c) => c.nome === "Direito")?.id,
      semestre: 2,
      inicioAcompanhamento: hojeCivil(new Date()),
      profissionalResponsavelId: anaId,
    },
  });
  expect(cadastro.ok()).toBe(true);
  const alunoId = ((await cadastro.json()) as { id: string }).id;

  // Documento restrito anexado no início, pela API: nunca deve chegar à coordenação.
  const restrito = await request.post(`/api/alunos/${alunoId}/documentos`, {
    headers: { ...auth, "Idempotency-Key": crypto.randomUUID() },
    multipart: { arquivo: { name: `restrito-${sufixo}.pdf`, mimeType: "application/pdf", buffer: PDF }, categoria: "Laudo", descricao: descricaoRestrita },
  });
  expect(restrito.ok()).toBe(true);

  // Profissional: anexa o relatório, compartilha com justificativa e escreve ao coordenador.
  await entrar(page, "ana.beatriz@nap.demo");
  await page.goto(`/alunos/${alunoId}`);
  await expect(page.getByRole("heading", { level: 1, name: nome })).toBeVisible();
  await page.getByRole("tab", { name: "Documentos" }).click();
  await page.getByRole("button", { name: "Anexar documento" }).click();
  const anexar = page.getByRole("dialog", { name: "Anexar documento" });
  await anexar.getByLabel("Arquivo").setInputFiles({ name: nomeArquivo, mimeType: "application/pdf", buffer: PDF });
  await anexar.getByLabel("Categoria").selectOption({ label: "Relatório" });
  await anexar.getByLabel("Descrição").fill(descricaoCompartilhada);
  await anexar.getByRole("button", { name: "Anexar", exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: /Documento anexado/ })).toBeVisible();

  await page.getByRole("button", { name: `Compartilhar ${descricaoCompartilhada}` }).click();
  const compartilhar = page.getByRole("dialog", { name: "Compartilhar com a coordenação" });
  await compartilhar.getByLabel("Justificativa").fill("Coordenação precisa acompanhar a devolutiva.");
  await compartilhar.getByRole("button", { name: "Compartilhar", exact: true }).click();
  await expect(page.getByRole("button", { name: `Revogar compartilhamento de ${descricaoCompartilhada}` })).toBeVisible();

  await page.getByRole("tab", { name: "Comunicações" }).click();
  await page.getByRole("button", { name: "Nova mensagem" }).click();
  const nova = page.getByRole("dialog", { name: "Nova mensagem" });
  await nova.getByLabel("Tipo").selectOption({ label: "Devolutiva" });
  await nova.getByLabel("Destinatário").selectOption({ label: "Marcos Vieira" });
  await nova.getByLabel("Assunto").fill(assunto);
  await nova.getByLabel("Mensagem").fill("Segue a devolutiva do acompanhamento.");
  await nova.getByRole("button", { name: "Enviar", exact: true }).click();
  await expect(page.getByRole("status").filter({ hasText: "Mensagem enviada." })).toBeVisible();
  await sair(page);

  // Coordenação de Direito: vê o contador, abre a mensagem, responde e baixa só o documento compartilhado.
  // Espera a notificação "Nova mensagem" existir: abrir a mensagem precisa baixar o selo em 2 (mensagem + notificação).
  const authCoord = { Authorization: `Bearer ${await tokenDe(request, "coord.direito@nap.demo")}` };
  const naoLidas = async (rota: string) => ((await (await request.get(rota, { headers: authCoord })).json()) as { naoLidas: number }).naoLidas;
  await expect(async () => {
    const avisos = (await (await request.get("/api/notificacoes?naoLidas=true&tamanho=100", { headers: authCoord })).json()) as { itens: { titulo: string }[] };
    expect(avisos.itens.some((n) => n.titulo === `Nova mensagem: ${assunto}`)).toBe(true);
  }).toPass({ timeout: 15_000 });
  const total = (await naoLidas("/api/comunicacoes/contagem")) + (await naoLidas("/api/notificacoes/contagem"));
  const selo = (n: number) => page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: new RegExp(`(^|\\D)${n} não lidas`) });

  await entrar(page, "coord.direito@nap.demo");
  await expect(selo(total)).toBeVisible();
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: /Comunicações/ }).click();
  await page.getByRole("link", { name: assunto }).click();
  await expect(page.getByRole("region", { name: "Conversa" }).getByText("Segue a devolutiva do acompanhamento.")).toBeVisible();
  if (total > 2) await expect(selo(total - 2)).toBeVisible();
  else await expect(page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: /não lidas/ })).toHaveCount(0);
  await page.getByRole("button", { name: "Responder" }).click();
  await page.getByRole("region", { name: "Responder" }).getByLabel("Resposta").fill(resposta);
  await page.getByRole("button", { name: "Enviar resposta" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Resposta enviada." })).toBeVisible();

  await page.goto(`/alunos/${alunoId}`);
  await page.getByRole("tab", { name: "Documentos" }).click();
  const tabela = page.getByRole("table", { name: "Documentos" });
  await expect(tabela.getByText(descricaoCompartilhada)).toBeVisible();
  await expect(tabela.getByText(descricaoRestrita)).toHaveCount(0);
  const [download] = await Promise.all([page.waitForEvent("download"), tabela.getByRole("button", { name: `Baixar ${nomeArquivo}` }).click()]);
  expect(download.suggestedFilename()).toBe(nomeArquivo);
  await sair(page);

  // Profissional: vê a resposta no fio.
  await entrar(page, "ana.beatriz@nap.demo");
  await aguardarSelo(page);
  await page.getByRole("navigation", { name: "Principal" }).getByRole("link", { name: /Comunicações/ }).click();
  await page.getByRole("link", { name: assunto }).click();
  await expect(page.getByRole("region", { name: "Conversa" }).getByText(resposta)).toBeVisible();
});
