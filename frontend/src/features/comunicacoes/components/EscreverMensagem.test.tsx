// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../../auth/useSessao";
import type { MensagemDetalhe } from "../../../lib/types";
import { comunicacaoService } from "../services/comunicacaoService";
import { EscreverMensagem } from "./EscreverMensagem";

vi.mock("../services/comunicacaoService", () => ({
  comunicacaoService: { destinatarios: vi.fn(), enviar: vi.fn(), criarRascunho: vi.fn(), atualizarRascunho: vi.fn() },
}));

const servico = vi.mocked(comunicacaoService);
const AVISO = "Não inclua conteúdo clínico em mensagens para a coordenação do curso.";

function montar(props: Partial<Parameters<typeof EscreverMensagem>[0]> = {}) {
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Ana Souza", email: "a@nap.demo", perfil: "ProfissionalNap", cursoId: null, curso: null, deveTrocarSenha: false },
    saiuPorVontade: false,
    entrar: vi.fn(),
    trocarSenha: vi.fn(),
    sair: vi.fn(),
  };
  const aoConcluir = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <SessaoContexto value={sessao}>
        <EscreverMensagem aoConcluir={aoConcluir} {...props} />
      </SessaoContexto>
    </QueryClientProvider>,
  );
  return { aoConcluir, user: userEvent.setup() };
}

const rascunho: MensagemDetalhe = {
  id: "r1",
  tipo: "Aviso",
  assunto: "Rascunho antigo",
  remetenteId: "u1",
  remetente: "Ana Souza",
  destinatarioId: "d2",
  destinatario: "Rita Coordenadora",
  alunoId: "a1",
  aluno: "Maria Clara",
  data: "2026-10-01T13:00:00Z",
  lida: false,
  recebida: false,
  corpo: "Texto salvo",
  encaminhamentoId: null,
  cursoId: null,
  estado: "Rascunho",
  podeResponder: false,
  podeMarcarLida: false,
  arquivada: false,
  fio: [],
  versao: "v7",
};

const nomesTipos = () =>
  within(screen.getByLabelText(/Tipo/))
    .getAllByRole("option")
    .map((o) => o.textContent);

beforeEach(() => {
  vi.resetAllMocks();
  servico.destinatarios.mockResolvedValue([
    { id: "d1", nome: "Carlos Mendes", perfil: "ProfissionalNap" },
    { id: "d2", nome: "Rita Coordenadora", perfil: "CoordenadorCurso" },
  ]);
  servico.enviar.mockResolvedValue({ id: "m9" });
});

describe("EscreverMensagem", () => {
  it("Destinatarios_vem_da_api_conforme_vinculos", async () => {
    montar({ aluno: { id: "a1", nome: "Maria Clara", matricula: "2026.0001" }, encaminhamentoId: "e1" });
    const destinatario = screen.getByLabelText(/Destinatário/);
    expect(await within(destinatario).findByRole("option", { name: /Carlos Mendes/ })).toBeInTheDocument();
    expect(servico.destinatarios).toHaveBeenCalledWith({ alunoId: "a1", encaminhamentoId: "e1" });
    // Com encaminhamento vinculado o retorno entra; envio de PEI nunca (Fatia D).
    expect(nomesTipos()).toContain("Retorno de encaminhamento");
    expect(nomesTipos()).not.toContain("Envio de PEI");
  });

  it("Sem_vinculo_nao_oferece_retorno_e_lista_vazia_avisa", async () => {
    servico.destinatarios.mockResolvedValue([]);
    montar();
    expect(await screen.findByText("Ninguém pode receber esta comunicação com estes vínculos.")).toBeInTheDocument();
    expect(servico.destinatarios).toHaveBeenCalledWith({});
    expect(nomesTipos()).not.toContain("Retorno de encaminhamento");
  });

  it("Aviso_de_conteudo_clinico_para_coordenador", async () => {
    const { user } = montar({ aluno: { id: "a1", nome: "Maria Clara" } });
    const destinatario = screen.getByLabelText(/Destinatário/);
    await within(destinatario).findByRole("option", { name: /Rita Coordenadora/ });
    expect(screen.queryByText(AVISO)).not.toBeInTheDocument();

    await user.selectOptions(destinatario, "d2");
    expect(screen.getByText(AVISO)).toBeInTheDocument();
    await user.selectOptions(destinatario, "d1");
    expect(screen.queryByText(AVISO)).not.toBeInTheDocument();
  });

  it("Enviar_manda_vinculos_com_chave_e_erro_de_campo_vai_ao_campo", async () => {
    servico.enviar.mockRejectedValueOnce({ status: 400, titulo: "Erro de validação", errosCampo: { destinatarioId: ["O destinatário não pode receber esta comunicação."] } });
    const { user, aoConcluir } = montar({ aluno: { id: "a1", nome: "Maria Clara" }, encaminhamentoId: "e1" });
    const destinatario = screen.getByLabelText(/Destinatário/);
    await within(destinatario).findByRole("option", { name: /Carlos Mendes/ });
    await user.selectOptions(destinatario, "d1");
    await user.selectOptions(screen.getByLabelText(/Tipo/), "Aviso");
    await user.type(screen.getByLabelText(/Assunto/), "Retorno da reunião");
    await user.type(screen.getByLabelText(/Mensagem/), "Linha 1{enter}Linha 2");
    await user.click(screen.getByRole("button", { name: "Enviar" }));

    expect(await screen.findByText("O destinatário não pode receber esta comunicação.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Enviar" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalledWith("enviada", "m9"));
    const [[body1, chave1], [body2, chave2]] = servico.enviar.mock.calls as unknown as [[unknown, string], [unknown, string]];
    expect(body1).toEqual({ rascunhoId: null, destinatarioId: "d1", tipo: "Aviso", assunto: "Retorno da reunião", corpo: "Linha 1\nLinha 2", cursoId: null, alunoId: "a1", encaminhamentoId: "e1" });
    expect(body2).toEqual(body1);
    expect(chave2).toBe(chave1);
  });

  it("Enviar_sem_destinatario_pede_o_destinatario", async () => {
    const { user } = montar();
    await user.selectOptions(screen.getByLabelText(/Tipo/), "Aviso");
    await user.type(screen.getByLabelText(/Assunto/), "Assunto qualquer");
    await user.type(screen.getByLabelText(/Mensagem/), "Corpo");
    await user.click(screen.getByRole("button", { name: "Enviar" }));
    expect(await screen.findByText("Selecione o destinatário.")).toBeInTheDocument();
    expect(servico.enviar).not.toHaveBeenCalled();
  });

  it("Enviar_com_tudo_vazio_marca_o_destinatario_junto_com_os_outros_campos", async () => {
    const { user } = montar();
    await within(screen.getByLabelText(/Destinatário/)).findByRole("option", { name: /Carlos Mendes/ });
    await user.click(screen.getByRole("button", { name: "Enviar" }));
    expect(await screen.findByText("Selecione o tipo.")).toBeInTheDocument();
    expect(screen.getByText("Selecione o destinatário.")).toBeInTheDocument();
    expect(servico.enviar).not.toHaveBeenCalled();
  });

  it("Falha_na_lista_de_destinatarios_oferece_tentar_novamente", async () => {
    servico.destinatarios.mockRejectedValueOnce(new Error("falhou"));
    const { user } = montar();
    await user.click(await screen.findByRole("button", { name: "Tentar novamente" }));
    expect(await within(screen.getByLabelText(/Destinatário/)).findByRole("option", { name: /Carlos Mendes/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Tentar novamente" })).not.toBeInTheDocument();
  });

  it("Rascunho_aberto_mostra_o_destinatario_quando_a_lista_chega_depois", async () => {
    let entregar: (v: Awaited<ReturnType<typeof comunicacaoService.destinatarios>>) => void = () => undefined;
    servico.destinatarios.mockReturnValue(new Promise((r) => (entregar = r)));
    montar({ rascunho });
    const destinatario = screen.getByLabelText(/Destinatário/);
    // Antes da lista: nada à vista e nada enviável.
    expect(destinatario).toBeDisabled();
    expect(screen.getByRole("button", { name: "Enviar" })).toBeDisabled();

    entregar([
      { id: "d1", nome: "Carlos Mendes", perfil: "ProfissionalNap" },
      { id: "d2", nome: "Rita Coordenadora", perfil: "CoordenadorCurso" },
    ]);
    await waitFor(() => expect(destinatario).toHaveValue("d2"));
    expect(screen.getByText(AVISO)).toBeInTheDocument();
  });

  it("Trocar_o_aluno_limpa_destinatario_que_deixou_de_ser_elegivel", async () => {
    servico.destinatarios.mockImplementation(async (f) =>
      f.alunoId
        ? [
            { id: "d1", nome: "Carlos Mendes", perfil: "ProfissionalNap" },
            { id: "d2", nome: "Rita Coordenadora", perfil: "CoordenadorCurso" },
          ]
        : [{ id: "d1", nome: "Carlos Mendes", perfil: "ProfissionalNap" }],
    );
    const { user } = montar({ rascunho });
    const destinatario = screen.getByLabelText(/Destinatário/);
    await waitFor(() => expect(destinatario).toHaveValue("d2"));

    await user.click(screen.getByRole("button", { name: "Trocar aluno" }));
    await waitFor(() => expect(servico.destinatarios).toHaveBeenCalledWith({}));
    await waitFor(() => expect(screen.getByLabelText(/Destinatário/)).toHaveValue(""));
    expect(screen.queryByText(AVISO)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Enviar" }));
    expect(await screen.findByText("Selecione o destinatário.")).toBeInTheDocument();
    expect(servico.enviar).not.toHaveBeenCalled();
  });

  it("Rascunho_salvo_usa_a_versao_da_abertura_e_envio_leva_o_rascunhoId", async () => {
    servico.atualizarRascunho.mockResolvedValue(undefined);
    servico.enviar.mockResolvedValue({ id: "r1" });
    const { user, aoConcluir } = montar({ rascunho });
    await waitFor(() => expect(screen.getByLabelText(/Destinatário/)).toHaveValue("d2"));

    await user.click(screen.getByRole("button", { name: "Salvar rascunho" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalledWith("rascunho", "r1"));
    expect(servico.atualizarRascunho).toHaveBeenCalledWith(
      "r1",
      { tipo: "Aviso", assunto: "Rascunho antigo", corpo: "Texto salvo", destinatarioId: "d2", cursoId: null, alunoId: "a1", encaminhamentoId: null },
      "v7",
    );
    expect(servico.criarRascunho).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Enviar" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalledWith("enviada", "r1"));
    // Enviar leva a versão da abertura: outra aba que salvou o rascunho antes recebe 409, não perde o texto.
    expect(servico.enviar).toHaveBeenCalledWith(expect.objectContaining({ rascunhoId: "r1", destinatarioId: "d2", alunoId: "a1" }), expect.any(String), "v7");
  });
});
