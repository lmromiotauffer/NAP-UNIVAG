// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { ToastProvider } from "../../components/ui/Toast";
import type { MensagemDetalhe } from "../../lib/types";
import { DetalheMensagemPage } from "./DetalheMensagemPage";
import { comunicacaoService } from "./services/comunicacaoService";

vi.mock("./services/comunicacaoService", () => ({
  comunicacaoService: { obter: vi.fn(), marcarLida: vi.fn(), contagem: vi.fn(), arquivar: vi.fn(), desarquivar: vi.fn(), responder: vi.fn() },
}));

const servico = vi.mocked(comunicacaoService);
const recebida: MensagemDetalhe = {
  id: "m1",
  tipo: "Aviso",
  assunto: "Reunião de acompanhamento",
  remetenteId: "u2",
  remetente: "Ana Souza",
  destinatarioId: "u1",
  destinatario: "Carlos Mendes",
  alunoId: null,
  aluno: null,
  data: "2026-10-01T13:00:00Z",
  lida: false,
  recebida: true,
  corpo: "<b>x</b>\nsegunda linha",
  encaminhamentoId: null,
  cursoId: null,
  estado: "Enviada",
  podeResponder: true,
  podeMarcarLida: true,
  arquivada: false,
  fio: [{ id: "m1", remetenteId: "u2", remetente: "Ana Souza", assunto: "Reunião de acompanhamento", corpo: "<b>x</b>\nsegunda linha", enviadaEm: "2026-10-01T13:00:00Z" }],
  versao: "v1",
};

function montar(voltar?: string) {
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Carlos Mendes", email: "c@nap.demo", perfil: "ProfissionalNap", cursoId: null, curso: null, deveTrocarSenha: false },
    saiuPorVontade: false,
    entrar: vi.fn(),
    trocarSenha: vi.fn(),
    sair: vi.fn(),
  };
  const router = createMemoryRouter([{ path: "/comunicacoes/:id", element: <DetalheMensagemPage /> }], { initialEntries: [{ pathname: "/comunicacoes/m1", state: voltar ? { voltar } : null }] });
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <SessaoContexto value={sessao}>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.resetAllMocks();
  servico.marcarLida.mockResolvedValue(undefined);
});

describe("DetalheMensagemPage", () => {
  it("Abrir_recebida_marca_lida_uma_vez", async () => {
    // O refetch após marcar ainda chega não lida (corrida com outra aba): não marca de novo.
    servico.obter.mockResolvedValue(recebida);
    montar();
    await screen.findByRole("heading", { name: "Reunião de acompanhamento" });
    await waitFor(() => expect(servico.obter.mock.calls.length).toBeGreaterThanOrEqual(2));
    expect(servico.marcarLida).toHaveBeenCalledTimes(1);
    expect(servico.marcarLida).toHaveBeenCalledWith("m1");
  });

  it("Falha_ao_marcar_lida_nao_bloqueia_a_leitura", async () => {
    servico.obter.mockResolvedValue(recebida);
    servico.marcarLida.mockRejectedValue(new Error("falhou"));
    montar();
    expect(await screen.findByRole("heading", { name: "Reunião de acompanhamento" })).toBeInTheDocument();
    await waitFor(() => expect(servico.marcarLida).toHaveBeenCalled());
    expect(screen.getByRole("button", { name: "Responder" })).toBeInTheDocument();
  });

  it("Enviada_por_mim_nao_marca_lida", async () => {
    servico.obter.mockResolvedValue({ ...recebida, recebida: false, podeMarcarLida: false, podeResponder: false, arquivada: true });
    montar();
    await screen.findByRole("heading", { name: "Reunião de acompanhamento" });
    expect(servico.marcarLida).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Responder" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Desarquivar" })).toBeInTheDocument();
  });

  it("Corpo_renderizado_como_texto", async () => {
    servico.obter.mockResolvedValue({ ...recebida, podeMarcarLida: false, lida: true });
    montar();
    const corpo = await screen.findByText(/<b>x<\/b>/);
    expect(corpo.textContent).toBe("<b>x</b>\nsegunda linha");
    expect(corpo).toHaveClass("whitespace-pre-wrap");
    expect(document.querySelector("b")).toBeNull();
  });

  it("Voltar_leva_a_aba_de_origem", async () => {
    servico.obter.mockResolvedValue({ ...recebida, podeMarcarLida: false, lida: true });
    montar("/comunicacoes?aba=arquivadas&pagina=2");
    expect(await screen.findByRole("link", { name: "Voltar para Comunicações" })).toHaveAttribute("href", "/comunicacoes?aba=arquivadas&pagina=2");
  });

  it.each(["//evil.example", "/\\evil.example", "https://evil.example"])("Voltar_externo_cai_em_comunicacoes: %s", async (voltar) => {
    servico.obter.mockResolvedValue({ ...recebida, podeMarcarLida: false, lida: true });
    montar(voltar);
    expect(await screen.findByRole("link", { name: "Voltar para Comunicações" })).toHaveAttribute("href", "/comunicacoes");
  });
});
