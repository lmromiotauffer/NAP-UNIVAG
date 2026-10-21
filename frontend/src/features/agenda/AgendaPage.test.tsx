// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../auth/useSessao";
import { ToastProvider } from "../../components/ui/Toast";
import { formatarDiaSemana, hojeCivil, segundaDoDia, somarDias } from "../../lib/format";
import { responderApi } from "../../test/apiFalsa";
import { AgendaPage } from "./AgendaPage";
import { agendaService } from "./services/agendaService";

const BRUNO = vi.hoisted(() => "22222222-2222-2222-2222-222222222222");
const EU = "33333333-3333-3333-3333-333333333333";

vi.mock("./services/agendaService", () => ({
  agendaService: {
    escalaSemana: vi.fn(),
    semana: vi.fn().mockResolvedValue({ inicio: "2026-09-28", fim: "2026-10-02", compromissos: [], atendimentosAvulsos: [], atividadesAvulsas: [] }),
    criar: vi.fn().mockResolvedValue({ id: "novo" }),
  },
}));
responderApi({
  "/usuarios/profissionais": [{ id: BRUNO, nome: "Bruno Lima", perfil: "ProfissionalNap" }],
});

/** Semana de 2026-09-14: quarta-feira feriado, quinta sem faixa, sexta "sem atendimento". */
const semanaDeEscala = (data: string) => ({
  inicio: data,
  dias: Array.from({ length: 7 }, (_, i) => {
    const dia = new Date(Date.parse(`${data}T12:00:00Z`) + i * 86_400_000).toISOString().slice(0, 10);
    const util = i < 5 && i !== 3;
    return { data: dia, inicio: util ? "07:00:00" : null, fim: util ? "17:00:00" : null, semAtendimento: i === 4, feriado: i === 2 ? "Independência" : null };
  }),
  fuso: "America/Cuiaba",
});

const sessao: ContextoSessao = {
  estado: "autenticado",
  usuario: { id: EU, nome: "Ana Beatriz", email: "a@nap.demo", perfil: "ProfissionalNap", cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false,
  entrar: vi.fn(),
  trocarSenha: vi.fn(),
  sair: vi.fn(),
};

describe("AgendaPage", () => {
  beforeEach(() => {
    vi.mocked(agendaService.escalaSemana).mockImplementation((semana) => Promise.resolve(semanaDeEscala(semana)));
  });

  it("Agenda_destaca_feriado_e_desabilita_dia_sem_faixa", async () => {
    const router = createMemoryRouter([{ path: "/agenda", element: <AgendaPage /> }], { initialEntries: ["/agenda?semana=2026-09-14"] });
    render(
      <QueryClientProvider client={new QueryClient()}>
        <SessaoContexto value={sessao}>
          <ToastProvider>
            <RouterProvider router={router} />
          </ToastProvider>
        </SessaoContexto>
      </QueryClientProvider>,
    );
    expect(await screen.findByRole("group", { name: "Quarta-feira, 16/09/2026, feriado: independência" })).toBeInTheDocument();
    expect(screen.getAllByText("Feriado: Independência").length).toBeGreaterThan(0);
    expect(screen.getByRole("group", { name: "Quinta-feira, 17/09/2026, fora da escala do profissional" })).toBeInTheDocument();
    expect(screen.getByRole("group", { name: "Sexta-feira, 18/09/2026, sem atendimento na escala" })).toBeInTheDocument();
    // Sábado e domingo sem faixa não viram coluna.
    expect(screen.queryByRole("group", { name: /^Sábado/ })).not.toBeInTheDocument();
    expect(agendaService.escalaSemana).toHaveBeenCalledWith("2026-09-14", EU);
  });

  it("compromisso_criado_vendo_colega_passa_a_exibir_a_agenda_de_quem_recebeu", async () => {
    const router = createMemoryRouter([{ path: "/agenda", element: <AgendaPage /> }], { initialEntries: [`/agenda?semana=2026-09-14&profissional=${BRUNO}`] });
    render(
      <QueryClientProvider client={new QueryClient()}>
        <SessaoContexto value={sessao}>
          <ToastProvider>
            <RouterProvider router={router} />
          </ToastProvider>
        </SessaoContexto>
      </QueryClientProvider>,
    );
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Novo compromisso" }));
    const d = within(screen.getByRole("dialog"));
    expect(d.getByText(/^Para:/)).toHaveTextContent("Para: você");
    await user.selectOptions(d.getByLabelText(/^Tipo/), "ReuniaoEquipe");
    await user.type(d.getByLabelText(/^Título/), "Reunião");
    await user.clear(d.getByLabelText(/^Data/));
    await user.type(d.getByLabelText(/^Data/), "2026-09-15");
    await user.type(d.getByLabelText(/^Início/), "09:00");
    await user.type(d.getByLabelText(/^Fim/), "10:00");
    await user.click(d.getByRole("button", { name: "Agendar" }));

    await waitFor(() => expect(agendaService.criar).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(new URLSearchParams(router.state.location.search).get("profissional")).toBe(EU));
    expect(new URLSearchParams(router.state.location.search).get("semana")).toBe("2026-09-14");
  });

  it("compromisso_criado_na_semana_atual_exibe_o_dia_dele_no_celular", async () => {
    // Terça da semana atual (segunda só quando hoje é terça): nunca o dia inicial da grade, nem no fim de semana,
    // em que o inicial já é segunda. Antes, o dia exibido ficava preso em "hoje".
    const hoje = hojeCivil();
    const segunda = segundaDoDia(hoje);
    const alvo = somarDias(segunda, hoje === somarDias(segunda, 1) ? 0 : 1);
    const router = createMemoryRouter([{ path: "/agenda", element: <AgendaPage /> }], { initialEntries: ["/agenda"] });
    render(
      <QueryClientProvider client={new QueryClient()}>
        <SessaoContexto value={sessao}>
          <ToastProvider>
            <RouterProvider router={router} />
          </ToastProvider>
        </SessaoContexto>
      </QueryClientProvider>,
    );
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Novo compromisso" }));
    const d = within(screen.getByRole("dialog"));
    await user.selectOptions(d.getByLabelText(/^Tipo/), "ReuniaoEquipe");
    await user.type(d.getByLabelText(/^Título/), "Reunião");
    await user.clear(d.getByLabelText(/^Data/));
    await user.type(d.getByLabelText(/^Data/), alvo);
    await user.type(d.getByLabelText(/^Início/), "09:00");
    await user.type(d.getByLabelText(/^Fim/), "10:00");
    await user.click(d.getByRole("button", { name: "Agendar" }));

    await waitFor(() => expect(agendaService.criar).toHaveBeenCalled());
    const dias = within(screen.getByRole("group", { name: "Dia exibido" }));
    await waitFor(() => expect(dias.getByRole("button", { pressed: true })).toHaveTextContent(formatarDiaSemana(alvo)));
  });
});
