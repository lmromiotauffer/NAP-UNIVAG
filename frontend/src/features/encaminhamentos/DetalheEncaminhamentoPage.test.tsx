// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { ToastProvider } from "../../components/ui/Toast";
import { hojeCivil, somarDias } from "../../lib/format";
import type { EncaminhamentoDetalhe } from "../../lib/types";
import { DetalheEncaminhamentoPage } from "./DetalheEncaminhamentoPage";
import { encaminhamentoService } from "./services/encaminhamentoService";

vi.mock("./services/encaminhamentoService", () => ({ encaminhamentoService: { obter: vi.fn() } }));

const obter = vi.mocked(encaminhamentoService.obter);

const base: EncaminhamentoDetalhe = {
  id: "e1",
  alunoId: "a1",
  aluno: "Maria Clara Souza",
  matricula: "2026.0001",
  curso: "Direito",
  especialidade: "PsicologiaEPsiquiatria",
  destino: "ClinicaEscola",
  estado: "Encaminhado",
  data: somarDias(hojeCivil(), -9),
  solicitante: "Ana Souza",
  semAcolhimentoNoPrazo: true,
  atualizadoEm: "2026-09-20T10:00:00-04:00",
  solicitanteId: "p1",
  motivoRestrito: "Crises de ansiedade antes das provas.",
  resumoCompartilhavel: "Acompanhamento psicológico na Clínica Escola.",
  observacoes: "Família ciente.",
  dataAcolhimento: null,
  dataPrimeiroAtendimento: null,
  dataEncerramento: null,
  podeRegistrarRetorno: true,
  podeCancelar: true,
  evolucoes: [],
  versao: "v1",
};

function montar(e: EncaminhamentoDetalhe, perfil = "CoordenadorNap") {
  obter.mockResolvedValue(e);
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Helena Prado", email: "h@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
    saiuPorVontade: false,
    entrar: vi.fn(),
    trocarSenha: vi.fn(),
    sair: vi.fn(),
  };
  const router = createMemoryRouter([{ path: "/encaminhamentos/:id", element: <DetalheEncaminhamentoPage /> }], { initialEntries: ["/encaminhamentos/e1"] });
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

const nomesAcoes = () =>
  screen
    .queryAllByRole("button")
    .map((b) => b.textContent)
    .filter((t) => t?.startsWith("Registrar") || t?.startsWith("Cancelar"));

beforeEach(() => {
  obter.mockReset();
});

describe("DetalheEncaminhamentoPage", () => {
  it("Acoes_respeitam_o_estado_e_as_permissoes_do_dto", async () => {
    montar(base);
    await screen.findByRole("heading", { name: "Maria Clara Souza" });
    expect(nomesAcoes()).toEqual(["Registrar acolhimento", "Registrar desistência", "Cancelar encaminhamento"]);
    // O selo de prazo acompanha o estado, sem substituí-lo.
    expect(screen.getByText("Encaminhado")).toBeInTheDocument();
    expect(screen.getByText("Sem acolhimento há 9 dias")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Maria Clara Souza" })).toHaveAttribute("href", "/alunos/a1");
  });

  it("Acoes_somem_quando_o_dto_nao_permite", async () => {
    montar({ ...base, estado: "EmAtendimento", semAcolhimentoNoPrazo: false, podeRegistrarRetorno: false, podeCancelar: false });
    await screen.findByRole("heading", { name: "Maria Clara Souza" });
    expect(nomesAcoes()).toEqual([]);
  });

  it("Em_atendimento_oferece_evolucao_alta_e_desistencia", async () => {
    montar({ ...base, estado: "EmAtendimento", semAcolhimentoNoPrazo: false, podeCancelar: false });
    await screen.findByRole("heading", { name: "Maria Clara Souza" });
    expect(nomesAcoes()).toEqual(["Registrar evolução", "Registrar alta", "Registrar desistência"]);
  });

  it("Detalhe_sem_motivo_nao_mostra_a_secao", async () => {
    montar({ ...base, motivoRestrito: null, observacoes: null }, "CoordenadorCurso");
    await screen.findByRole("heading", { name: "Maria Clara Souza" });
    expect(screen.queryByRole("heading", { name: /Motivo restrito/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /Observações/ })).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /Resumo compartilhável/ })).toBeInTheDocument();
  });

  it("Especialidade_e_destino_ocultos_mostram_rotulo_neutro", async () => {
    montar({ ...base, especialidade: null, destino: null, motivoRestrito: null, observacoes: null }, "CoordenadorCurso");
    await screen.findByRole("heading", { name: "Maria Clara Souza" });
    expect(screen.getByText(/Não compartilhado com o curso/)).toBeInTheDocument();
  });

  it("Nota_do_motivo_segue_o_destino", async () => {
    montar(base);
    expect(await screen.findByText("Visível só à equipe NAP e ao SIP/Clínica Escola.")).toBeInTheDocument();
  });

  it("Nota_do_motivo_sem_servico_de_destino_fala_so_da_equipe", async () => {
    montar({ ...base, especialidade: "CoordenacaoCurso", destino: "CoordenacaoCurso" });
    expect(await screen.findByText("Visível só à equipe NAP.")).toBeInTheDocument();
  });

  it("Nome_do_aluno_so_vira_link_com_acesso_a_ficha", async () => {
    montar(base, "Sip");
    await screen.findByRole("heading", { name: "Maria Clara Souza" });
    expect(screen.queryByRole("link", { name: "Maria Clara Souza" })).not.toBeInTheDocument();
  });
});
