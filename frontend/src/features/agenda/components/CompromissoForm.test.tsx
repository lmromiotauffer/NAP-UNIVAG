// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { responderApi } from "../../../test/apiFalsa";
import { agendaService } from "../services/agendaService";
import { CompromissoForm } from "./CompromissoForm";

const BRUNO = vi.hoisted(() => "22222222-2222-2222-2222-222222222222");
const ANA = vi.hoisted(() => "11111111-1111-1111-1111-111111111111");

vi.mock("../services/agendaService", () => ({ agendaService: { criar: vi.fn().mockResolvedValue({ id: "novo" }) } }));
responderApi({
  "/usuarios/profissionais": [
    { id: BRUNO, nome: "Bruno Lima", perfil: "ProfissionalNap" },
    { id: ANA, nome: "Ana Souza", perfil: "ProfissionalNap" },
  ],
});

describe("CompromissoForm", () => {
  it("profissional_enviado_e_o_exibido_quando_o_padrao_nao_esta_na_lista", async () => {
    const user = userEvent.setup();
    const aoConcluir = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CompromissoForm coordenacao profissionalPadrao="fora-da-lista" aoConcluir={aoConcluir} />
      </QueryClientProvider>,
    );
    const profissional = await screen.findByLabelText(/^Profissional/);
    await waitFor(() => expect(profissional).toHaveValue(BRUNO));
    await user.selectOptions(screen.getByLabelText(/^Tipo/), "ReuniaoEquipe");
    await user.type(screen.getByLabelText(/^Título/), "Reunião");
    await user.type(screen.getByLabelText(/^Data/), "2026-09-29");
    await user.type(screen.getByLabelText(/^Início/), "09:00");
    await user.type(screen.getByLabelText(/^Fim/), "10:00");
    await user.click(screen.getByRole("button", { name: "Agendar" }));

    await waitFor(() => expect(agendaService.criar).toHaveBeenCalledTimes(1));
    expect(vi.mocked(agendaService.criar).mock.calls[0]?.[0]).toMatchObject({ profissionalId: BRUNO, alunoId: null });
    // A agenda usa o profissional efetivo para exibir quem recebeu o compromisso.
    expect(aoConcluir).toHaveBeenCalledWith("novo", expect.any(String), BRUNO);
  });

  it("profissional_nap_ve_para_voce_e_recebe_profissional_nulo", async () => {
    vi.mocked(agendaService.criar).mockClear();
    const user = userEvent.setup();
    const aoConcluir = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CompromissoForm coordenacao={false} profissionalPadrao={BRUNO} aoConcluir={aoConcluir} />
      </QueryClientProvider>,
    );
    expect(screen.getByText(/^Para:/)).toHaveTextContent("Para: você");
    expect(screen.queryByLabelText(/^Profissional/)).not.toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText(/^Tipo/), "ReuniaoEquipe");
    await user.type(screen.getByLabelText(/^Título/), "Reunião");
    await user.type(screen.getByLabelText(/^Data/), "2026-09-29");
    await user.type(screen.getByLabelText(/^Início/), "09:00");
    await user.type(screen.getByLabelText(/^Fim/), "10:00");
    await user.click(screen.getByRole("button", { name: "Agendar" }));

    await waitFor(() => expect(aoConcluir).toHaveBeenCalledWith("novo", expect.any(String), null));
    expect(vi.mocked(agendaService.criar).mock.calls[0]?.[0]).toMatchObject({ profissionalId: null });
  });

  it("sabado_e_termino_apos_o_fechamento_passam_pelo_formulario_e_chegam_ao_servico", async () => {
    vi.mocked(agendaService.criar).mockClear();
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CompromissoForm coordenacao={false} profissionalPadrao={BRUNO} aoConcluir={vi.fn()} />
      </QueryClientProvider>,
    );
    await user.selectOptions(screen.getByLabelText(/^Tipo/), "ReuniaoEquipe");
    await user.type(screen.getByLabelText(/^Título/), "Plantão");
    await user.type(screen.getByLabelText(/^Data/), "2026-10-03");
    await user.type(screen.getByLabelText(/^Início/), "16:30");
    await user.type(screen.getByLabelText(/^Fim/), "18:00");
    await user.click(screen.getByRole("button", { name: "Agendar" }));

    // Escala e feriado são do servidor (422); o cliente só confere formato e fim > início.
    await waitFor(() => expect(agendaService.criar).toHaveBeenCalledTimes(1));
  });

  it("Bloqueio_em_conflito_mostra_a_lista", async () => {
    vi.mocked(agendaService.criar).mockRejectedValueOnce({
      status: 409,
      titulo: "Conflito de horário com outro compromisso, atendimento ou atividade do profissional ou do aluno",
      errosCampo: {},
      conflitos: [{ id: "c1", tipo: "Atendimento", inicio: "2026-09-29T12:00:00Z", fim: "2026-09-29T13:00:00Z", aluno: "Maria da Silva" }],
    });
    const user = userEvent.setup();
    const aoConcluir = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CompromissoForm coordenacao={false} profissionalPadrao={BRUNO} aoConcluir={aoConcluir} />
      </QueryClientProvider>,
    );
    await user.selectOptions(screen.getByLabelText(/^Tipo/), "Bloqueio");
    await user.type(screen.getByLabelText(/^Título/), "Bloqueio");
    await user.type(screen.getByLabelText(/^Data/), "2026-09-29");
    await user.type(screen.getByLabelText(/^Início/), "09:00");
    await user.type(screen.getByLabelText(/^Fim/), "10:00");
    await user.click(screen.getByRole("button", { name: "Agendar" }));

    const lista = await screen.findByRole("list", { name: "Itens em conflito" });
    expect(lista).toHaveTextContent("Atendimento");
    expect(lista).toHaveTextContent("Maria da Silva");
    expect(aoConcluir).not.toHaveBeenCalled();
  });

  async function preencher(user: ReturnType<typeof userEvent.setup>, tipo: string) {
    await user.selectOptions(screen.getByLabelText(/^Tipo/), tipo);
    await user.type(screen.getByLabelText(/^Título/), "Reunião");
    await user.type(screen.getByLabelText(/^Data/), "2026-09-29");
    await user.type(screen.getByLabelText(/^Início/), "09:00");
    await user.type(screen.getByLabelText(/^Fim/), "10:00");
  }

  it("Reuniao_de_equipe_envia_participantes", async () => {
    vi.mocked(agendaService.criar).mockClear();
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CompromissoForm coordenacao={false} usuarioId={ANA} aoConcluir={vi.fn()} />
      </QueryClientProvider>,
    );
    await preencher(user, "ReuniaoEquipe");
    // O próprio usuário já vem marcado e não pode sair da lista.
    expect(await screen.findByRole("checkbox", { name: /^Ana Souza/ })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: /^Ana Souza/ })).toBeDisabled();
    expect(screen.getByRole("checkbox", { name: /^Ana Souza/ })).toHaveAccessibleDescription("Você precisa participar do grupo que cria");
    await user.click(await screen.findByRole("checkbox", { name: "Bruno Lima" }));
    await user.click(screen.getByRole("button", { name: "Agendar" }));

    await waitFor(() => expect(agendaService.criar).toHaveBeenCalledTimes(1));
    const corpo = vi.mocked(agendaService.criar).mock.calls[0]?.[0];
    expect([...(corpo?.participantesIds ?? [])].sort()).toEqual([ANA, BRUNO]);
    expect(corpo).toMatchObject({ profissionalId: null, alunoId: null });
  });

  it("Toda_a_equipe_envia_flag", async () => {
    vi.mocked(agendaService.criar).mockClear();
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CompromissoForm coordenacao={false} usuarioId={ANA} aoConcluir={vi.fn()} />
      </QueryClientProvider>,
    );
    await preencher(user, "Lembrete");
    await user.click(await screen.findByRole("checkbox", { name: "Toda a equipe" }));
    await user.click(screen.getByRole("button", { name: "Agendar" }));

    await waitFor(() => expect(agendaService.criar).toHaveBeenCalledTimes(1));
    const corpo = vi.mocked(agendaService.criar).mock.calls[0]?.[0];
    expect(corpo).toMatchObject({ todaEquipe: true, profissionalId: null });
    expect(corpo?.participantesIds).toBeUndefined();
  });

  it("Tipo_atendimento_nao_mostra_participantes", async () => {
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <CompromissoForm coordenacao={false} usuarioId={ANA} aoConcluir={vi.fn()} />
      </QueryClientProvider>,
    );
    expect(screen.queryByRole("checkbox", { name: "Toda a equipe" })).not.toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText(/^Tipo/), "ReuniaoEquipe");
    expect(await screen.findByRole("checkbox", { name: "Toda a equipe" })).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText(/^Tipo/), "Atendimento");
    expect(screen.queryByRole("checkbox", { name: "Toda a equipe" })).not.toBeInTheDocument();
  });
});
