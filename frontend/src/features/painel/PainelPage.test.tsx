// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import { createMemoryRouter, MemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import type { Painel } from "../../lib/types";
import { responderApi } from "../../test/apiFalsa";
import { BlocoPendencias } from "./components/BlocoPendencias";
import { PainelPage, serieSeisMeses } from "./PainelPage";

const BRUNO = vi.hoisted(() => "22222222-2222-2222-2222-222222222222");
const EU = "33333333-3333-3333-3333-333333333333";

const painel: Painel = {
  periodo: { inicio: "2026-09-28", fim: "2026-10-02", parcial: true },
  semana: null,
  agendaHoje: [],
  ultimos6Meses: null,
  alunosPorCurso: [],
  alunosPorSituacao: [],
  alertas: null,
  porProfissional: [],
  encaminhamentos: null,
};

responderApi({
  "/painel": painel,
  "/cursos": [],
  "/usuarios/profissionais": [{ id: BRUNO, nome: "Bruno Lima", perfil: "ProfissionalNap" }],
  "/comunicacoes/contagem": { naoLidas: 4 },
});

const sessao: ContextoSessao = {
  estado: "autenticado",
  usuario: { id: EU, nome: "Helena Prado", email: "h@nap.demo", perfil: "CoordenadorNap", cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
};

function montar(busca: string, s: ContextoSessao = sessao) {
  const router = createMemoryRouter([{ path: "/", element: <PainelPage /> }], { initialEntries: [`/${busca}`] });
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SessaoContexto value={s}>
        <RouterProvider router={router} />
      </SessaoContexto>
    </QueryClientProvider>,
  );
}

// Notas dos blocos que ignoram o filtro de profissional (o select também diz "toda a equipe", sem o "·").
const NOTA_EQUIPE = /· toda a equipe$/;

describe("PainelPage: escopo exibido", () => {
  it("filtro_de_outro_profissional_avisa_blocos_da_equipe_e_nomeia_a_agenda", async () => {
    montar(`?profissionalId=${BRUNO}`);
    expect(await screen.findByRole("heading", { name: "Agenda de hoje · Bruno Lima" })).toBeInTheDocument();
    // Alunos por curso, situação e produção por profissional.
    expect(screen.getAllByText(NOTA_EQUIPE)).toHaveLength(3);
  });

  it("filtro_do_proprio_usuario_e_sua_agenda", async () => {
    montar(`?profissionalId=${EU}`);
    expect(await screen.findByRole("heading", { name: "Sua agenda hoje" })).toBeInTheDocument();
  });

  it("sem_filtro_e_a_agenda_da_equipe_sem_aviso", async () => {
    montar("");
    expect(await screen.findByRole("heading", { name: "Agenda de hoje · equipe" })).toBeInTheDocument();
    expect(screen.queryAllByText(NOTA_EQUIPE)).toHaveLength(0);
  });
});

describe("serieSeisMeses", () => {
  it("serie_da_api_vira_barras_sem_preencher_nem_converter_nulo", () => {
    const dados = serieSeisMeses([
      { ano: 2026, mes: 8, realizados: 0, minutos: 0, parcial: false },
      { ano: 2026, mes: 9, realizados: null, minutos: null, parcial: false },
      { ano: 2026, mes: 10, realizados: 9, minutos: 400, parcial: true },
    ]);
    expect(dados).toEqual([
      { rotulo: "ago", valor: 0, parcial: false },
      { rotulo: "set", valor: null, parcial: false },
      { rotulo: "out", valor: 9, parcial: true },
    ]);
  });
});

describe("BlocoPendencias", () => {
  it("links_de_ficha_tem_nomes_acessiveis_distintos_com_o_aluno", () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter>
          <BlocoPendencias
            alertas={[
              { tipo: "faltas_recorrentes", titulo: "Faltas recorrentes", detalhe: "Ana Souza: 3 faltas consecutivas", alunoId: "a1", diasOuOcorrencias: 3 },
              { tipo: "faltas_recorrentes", titulo: "Faltas recorrentes", detalhe: "Caio Lima: 4 faltas nos últimos 30 dias", alunoId: "a2", diasOuOcorrencias: 4 },
            ]}
          />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    expect(screen.getByRole("link", { name: /^Abrir ficha: Ana Souza/ })).toHaveAttribute("href", "/alunos/a1");
    expect(screen.getByRole("link", { name: /^Abrir ficha: Caio Lima/ })).toHaveAttribute("href", "/alunos/a2");
  });
});

describe("PainelPage: encaminhamentos", () => {
  const sip: ContextoSessao = { ...sessao, usuario: { id: EU, nome: "Sonia Alves", email: "s@nap.demo", perfil: "Sip", cursoId: null, curso: null, deveTrocarSenha: false } };
  const encaminhamentos = { abertos: 7, aguardandoAcolhimento: 3, emAtendimento: 2, semAtualizacao: 1, semAcolhimentoNoPrazo: 2, diasSemAtualizacao: 15 };

  it("Painel_do_sip_mostra_os_quatro_indicadores", async () => {
    responderApi({
      "/painel": {
        ...painel,
        agendaHoje: null,
        alunosPorCurso: null,
        alunosPorSituacao: null,
        porProfissional: null,
        encaminhamentos,
        alertas: [{ tipo: "encaminhamento-sem-acolhimento", titulo: "Encaminhamento sem acolhimento", detalhe: "Ana Souza: encaminhado há 9 dias sem acolhimento", alunoId: "a1", diasOuOcorrencias: 9 }],
      },
      "/comunicacoes/contagem": { naoLidas: 0 },
    });
    montar("", sip);

    const secao = await screen.findByRole("region", { name: "Encaminhamentos" });
    expect(within(secao).getByText("Recebidos (abertos)")).toBeInTheDocument();
    expect(within(secao).getByText("Aguardando acolhimento")).toBeInTheDocument();
    expect(within(secao).getByText("Em atendimento")).toBeInTheDocument();
    expect(within(secao).getByText("Sem atualização recente")).toBeInTheDocument();
    expect(within(secao).getByText("7")).toBeInTheDocument();
    expect(within(secao).getByText("2 fora do prazo de acolhimento")).toBeInTheDocument();
    expect(within(secao).getByText(/mais de 15 dias/)).toBeInTheDocument();
    // O SIP não abre ficha de aluno: o alerta leva à lista de encaminhamentos.
    expect(screen.getByText("Ana Souza: encaminhado há 9 dias sem acolhimento")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Abrir ficha/ })).not.toBeInTheDocument();
  });

  it("Painel_mostra_o_limite_vindo_da_api", async () => {
    responderApi({
      "/painel": { ...painel, agendaHoje: null, alunosPorCurso: null, alunosPorSituacao: null, porProfissional: null, encaminhamentos: { ...encaminhamentos, diasSemAtualizacao: 21 } },
      "/comunicacoes/contagem": { naoLidas: 0 },
    });
    montar("", sip);

    const secao = await screen.findByRole("region", { name: "Encaminhamentos" });
    expect(within(secao).getByText("sem retorno há mais de 21 dias")).toBeInTheDocument();
  });

  it("Painel_sem_bloco_de_encaminhamentos_nao_renderiza_secao", async () => {
    responderApi({ "/painel": { ...painel, encaminhamentos: null }, "/cursos": [], "/usuarios/profissionais": [], "/comunicacoes/contagem": { naoLidas: 0 } });
    montar("");

    expect(await screen.findByRole("heading", { name: "Agenda de hoje · equipe" })).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Encaminhamentos" })).not.toBeInTheDocument();
  });
});

describe("PainelPage: atividades", () => {
  it("Painel_mostra_atividades_do_mes", async () => {
    responderApi({
      "/painel": {
        ...painel,
        atividades: {
          noMes: 3,
          minutosNoMes: 270,
          recentes: [
            { id: "at1", inicio: "2026-10-01T08:00:00-04:00", turno: "Manha", duracaoMinutos: 90, curso: "Direito", profissional: "Ana Souza", tipo: "RodaConversa", descricao: "Acolhimento" },
            { id: "at2", inicio: "2026-09-30T19:00:00-04:00", turno: "Noite", duracaoMinutos: 60, curso: null, profissional: "Ana Souza", tipo: "FormacaoDocente", descricao: "Formação" },
          ],
        },
      },
      "/cursos": [],
      "/usuarios/profissionais": [],
      "/comunicacoes/contagem": { naoLidas: 0 },
    });
    montar("");

    const secao = await screen.findByRole("region", { name: "Atividades nos cursos" });
    expect(within(secao).getByText("Atividades no mês")).toBeInTheDocument();
    expect(within(secao).getByText("3")).toBeInTheDocument();
    expect(within(secao).getByText("4h30")).toBeInTheDocument();
    const recentes = within(secao).getAllByRole("listitem");
    expect(recentes).toHaveLength(2);
    expect(recentes[0]).toHaveTextContent("Direito");
    expect(recentes[0]).toHaveTextContent("Roda de conversa · 01/10/2026 · Manhã 8h00");
    expect(recentes[1]).toHaveTextContent("Institucional");
    expect(recentes[1]).toHaveTextContent("Noite 19h00");
  });

  it("Painel_sem_bloco_de_atividades_nao_renderiza_secao", async () => {
    responderApi({ "/painel": painel, "/cursos": [], "/usuarios/profissionais": [], "/comunicacoes/contagem": { naoLidas: 0 } });
    montar("");
    await screen.findByRole("heading", { name: "Agenda de hoje · equipe" });
    expect(screen.queryByRole("region", { name: "Atividades nos cursos" })).not.toBeInTheDocument();
  });
});

describe("PainelPage: comunicações", () => {
  it("Cartao_de_mensagens_nao_lidas_vem_da_contagem", async () => {
    responderApi({ "/painel": painel, "/cursos": [], "/usuarios/profissionais": [], "/comunicacoes/contagem": { naoLidas: 4 } });
    montar("");
    const botao = await screen.findByRole("button", { name: "Detalhes: Mensagens não lidas" });
    expect(within(botao.parentElement!).getByText("4")).toBeInTheDocument();
  });

  it("Gestao_nao_ve_o_cartao_de_mensagens", async () => {
    const gestao: ContextoSessao = { ...sessao, usuario: { id: EU, nome: "Gil Souza", email: "g@nap.demo", perfil: "Gestao", cursoId: null, curso: null, deveTrocarSenha: false } };
    responderApi({ "/painel": painel, "/cursos": [], "/usuarios/profissionais": [] });
    montar("", gestao);
    expect(await screen.findByRole("heading", { name: /Gil\./ })).toBeInTheDocument();
    expect(screen.queryByText("Mensagens não lidas")).not.toBeInTheDocument();
  });
});
