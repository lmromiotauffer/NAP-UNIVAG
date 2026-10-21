// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosAdapter } from "axios";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { ToastProvider } from "../../components/ui/Toast";
import { api } from "../../lib/api";
import type { Producao } from "../../lib/types";
import { hojeCivil } from "../../lib/format";
import { responderApi } from "../../test/apiFalsa";
import { producao } from "../../test/producao";
import { ProducaoPage } from "./ProducaoPage";

function montar(perfil: string, dados: Producao, url = "/") {
  responderApi({ "/producao": dados, "/cursos": [{ id: "cu1", nome: "Direito", ativo: true, versao: "v" }], "/usuarios/profissionais": [{ id: "p1", nome: "Ana Souza" }] });
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Teste Silva", email: "t@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
    saiuPorVontade: false,
    entrar: vi.fn(),
    trocarSenha: vi.fn(),
    sair: vi.fn(),
  };
  const router = createMemoryRouter([{ path: "/", element: <ProducaoPage /> }], { initialEntries: [url] });
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoContexto value={sessao}>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
}

const adapterOriginal = api.defaults.adapter;
const urlOriginal = { criar: URL.createObjectURL, revogar: URL.revokeObjectURL };
afterEach(() => {
  URL.createObjectURL = urlOriginal.criar;
  URL.revokeObjectURL = urlOriginal.revogar;
  api.defaults.adapter = adapterOriginal;
  vi.restoreAllMocks();
});

/** Troca só a rota de exportação: guarda a URL pedida e responde com arquivo ou ProblemDetails. */
function falsearExportacao(erro?: { status: number; detalhe: string }) {
  const urls: string[] = [];
  const base = api.defaults.adapter as AxiosAdapter;
  api.defaults.adapter = async (config) => {
    if (!(config.url ?? "").startsWith("/producao/exportar")) return base(config);
    urls.push(config.url ?? "");
    if (erro) {
      const data = new Blob([JSON.stringify({ title: "Erro", detail: erro.detalhe })]);
      throw new AxiosError("falhou", String(erro.status), config, null, { status: erro.status, statusText: "", data, headers: {}, config });
    }
    return { status: 200, statusText: "", data: new Blob(["x"]), headers: { "content-disposition": "attachment; filename*=UTF-8''Producao-Mes-2026-09-01.pdf" }, config };
  };
  return urls;
}

describe("ProducaoPage", () => {
  it("Exportar_pdf_usa_os_filtros_da_tela", async () => {
    URL.createObjectURL = vi.fn(() => "blob:teste");
    URL.revokeObjectURL = vi.fn();
    const baixados: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
      baixados.push(this.download);
    });
    montar("Administrador", producao(), "/?tipo=Semestre&referencia=2026-09-15&profissionalId=p1&cursoId=cu1");
    const urls = falsearExportacao();
    await userEvent.click(await screen.findByRole("button", { name: "Exportar PDF" }));
    await waitFor(() => expect(baixados).toEqual(["Producao-Mes-2026-09-01.pdf"]));
    expect(urls).toEqual(["/producao/exportar?formato=pdf&tipo=Semestre&referencia=2026-09-15&profissionalId=p1&cursoId=cu1"]);
  });

  it("Erro_na_exportacao_mostra_mensagem_e_nao_baixa", async () => {
    URL.createObjectURL = vi.fn(() => "blob:teste");
    const clique = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);
    montar("Administrador", producao());
    falsearExportacao({ status: 403, detalhe: "Sem permissão para exportar." });
    await userEvent.click(await screen.findByRole("button", { name: "Exportar Excel" }));
    expect(await screen.findByText("Sem permissão para exportar.")).toBeInTheDocument();
    expect(URL.createObjectURL).not.toHaveBeenCalled();
    expect(clique).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Exportar Excel" })).toBeEnabled();
  });

  it("Taxa_mostra_a_base_ou_o_motivo", async () => {
    montar("ProfissionalNap", producao());
    expect(await screen.findByText(/3 de 25 atendimentos individuais \+ faltas/)).toBeInTheDocument();
    expect(screen.getByText("12,0%")).toBeInTheDocument();
  });

  it("Taxa_sem_denominador_mostra_o_motivo", async () => {
    montar("ProfissionalNap", producao({ taxaAusencia: { valor: null, numerador: 0, denominador: 0, motivo: "Sem atendimentos individuais no período." } }));
    expect(await screen.findByText("Sem atendimentos individuais no período.")).toBeInTheDocument();
  });

  it("Coordenador_de_curso_nao_ve_quadro_de_tempo", async () => {
    montar("CoordenadorCurso", producao({ tempo: null, pendentes: null }));
    expect(await screen.findByRole("table", { name: "Produção por categoria" })).toBeInTheDocument();
    expect(screen.queryByText("Capacidade e tempo")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Profissional")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Curso")).not.toBeInTheDocument();
  });

  it("Equipe_ve_quadro_de_tempo_e_nao_tem_filtro_de_profissional", async () => {
    montar("ProfissionalNap", producao());
    expect(await screen.findByText("Capacidade e tempo")).toBeInTheDocument();
    expect(screen.getByText("Ociosidade")).toBeInTheDocument();
    expect(screen.queryByLabelText("Profissional")).not.toBeInTheDocument();
  });

  it("Inconsistente_aparece_em_destaque_e_periodo_parcial_e_avisado", async () => {
    const base = producao();
    montar("Administrador", producao({ periodo: { ...base.periodo, parcial: true }, tempo: { ...base.tempo!, ociosidade: -30, inconsistente: true, diasInconsistentes: ["2026-09-29"] } }));
    expect(await screen.findByText("Inconsistente:")).toBeInTheDocument();
    expect(screen.getByText(/Período parcial/)).toBeInTheDocument();
    expect(screen.getByText("-30 min")).toBeInTheDocument();
    expect(screen.getByLabelText("Profissional")).toBeInTheDocument();
    expect(screen.getByLabelText("Curso")).toBeInTheDocument();
  });

  it("Comparacao_sem_base_mostra_traco", async () => {
    montar("ProfissionalNap", producao());
    const tabela = await screen.findByRole("table", { name: "Variação sobre o período anterior" });
    expect(within(tabela).getAllByText(/\(—\)/).length).toBeGreaterThan(0);
  });

  it("Grafico_tem_rotulos_acessiveis", async () => {
    montar("ProfissionalNap", producao());
    expect(await screen.findByRole("img", { name: "1–6: 22 ocorrências, 22h00" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "7–13 (parcial): 22 ocorrências, 22h00" })).toBeInTheDocument();
  });

  it("Gestao_ve_filtro_de_profissional_e_a_comparacao_mas_nao_o_de_curso", async () => {
    const base = producao();
    montar("Gestao", producao({ porProfissional: [{ profissionalId: "p1", profissional: "Ana Souza", categorias: base.categorias, tempo: base.tempo, taxaAusencia: base.taxaAusencia, pendentes: 0 }] }));
    expect(await screen.findByRole("table", { name: "Comparação entre profissionais" })).toBeInTheDocument();
    expect(screen.getByLabelText("Profissional")).toBeInTheDocument();
    expect(screen.queryByLabelText("Curso")).not.toBeInTheDocument();
  });

  it("Proximo_periodo_so_desabilita_quando_o_periodo_atual_alcanca_hoje", async () => {
    const hoje = hojeCivil();
    const base = producao();
    // Semestre que ainda termina no futuro: o próximo período (início depois de hoje) não existe.
    montar("ProfissionalNap", producao({ periodo: { ...base.periodo, fim: hoje } }));
    expect(await screen.findByRole("button", { name: "Próximo período" })).toBeDisabled();
  });

  it("Proximo_periodo_fica_habilitado_quando_o_periodo_atual_ja_terminou", async () => {
    montar("ProfissionalNap", producao({ periodo: { tipo: "Mes", inicio: "2020-01-01", fim: "2020-01-31", parcial: false } }));
    await screen.findByText("01/01/2020 a 31/01/2020");
    expect(screen.getByRole("button", { name: "Próximo período" })).toBeEnabled();
  });
});
