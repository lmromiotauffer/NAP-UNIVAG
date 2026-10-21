// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../../../auth/useSessao";
import { ToastProvider } from "../../../components/ui/Toast";
import { responderApi } from "../../../test/apiFalsa";
import { administracaoService } from "../services/administracaoService";
import { EscalaForm } from "./EscalaForm";
import { EscalasTab } from "./EscalasTab";
import { previaEscala } from "./previaEscala";

vi.mock("../services/administracaoService", () => ({ administracaoService: { listarEscalas: vi.fn(), criarEscala: vi.fn(), vigenciaEscala: vi.fn() } }));

responderApi({ "/usuarios/profissionais": [{ id: "p1", nome: "Ana Souza", perfil: "ProfissionalNap" }] });

const cliente = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });

describe("EscalaForm", () => {
  const aoConcluir = vi.fn();
  beforeEach(() => {
    vi.mocked(administracaoService.criarEscala).mockReset();
    aoConcluir.mockReset();
  });

  function montar() {
    render(
      <QueryClientProvider client={cliente()}>
        <EscalaForm aoConcluir={aoConcluir} aoCancelar={vi.fn()} />
      </QueryClientProvider>,
    );
    return userEvent.setup();
  }

  it("Escala_exige_vigencia_e_horario_valido", async () => {
    const user = montar();
    await user.click(screen.getByRole("button", { name: "Salvar" }));
    expect(await screen.findByText("Selecione o profissional.")).toBeInTheDocument();
    expect(screen.getByText("Informe o início da vigência.")).toBeInTheDocument();

    // Fim antes do início e horário fora de múltiplos de 15 minutos.
    fireEvent.change(screen.getByLabelText("Fim de Segunda-feira"), { target: { value: "06:00" } });
    fireEvent.change(screen.getByLabelText("Início de Terça-feira"), { target: { value: "07:10" } });
    await user.click(screen.getByRole("button", { name: "Salvar" }));
    expect(await screen.findByText("Segunda-feira: O fim deve ser depois do início.")).toBeInTheDocument();
    expect(screen.getByText("Terça-feira: Use horários em múltiplos de 15 minutos.")).toBeInTheDocument();

    // Com data de término marcada, o fim da vigência passa a ser obrigatório e não pode preceder o início.
    await user.click(screen.getByLabelText("Sem data de término"));
    fireEvent.change(screen.getByLabelText(/^Início da vigência/), { target: { value: "2026-10-05" } });
    fireEvent.change(screen.getByLabelText(/^Fim da vigência/), { target: { value: "2026-10-01" } });
    await user.click(screen.getByRole("button", { name: "Salvar" }));
    expect(await screen.findByText("O fim da vigência não pode ser anterior ao início.")).toBeInTheDocument();
    expect(administracaoService.criarEscala).not.toHaveBeenCalled();
  });

  it("Escala_valida_vai_para_a_api_com_as_faixas_marcadas", async () => {
    vi.mocked(administracaoService.criarEscala).mockResolvedValue({ id: "e1" });
    const user = montar();
    await screen.findByRole("option", { name: "Ana Souza" });
    await user.selectOptions(screen.getByLabelText(/^Profissional/), "p1");
    fireEvent.change(screen.getByLabelText(/^Início da vigência/), { target: { value: "2026-10-05" } });
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    await waitFor(() => expect(administracaoService.criarEscala).toHaveBeenCalledTimes(1));
    const [body] = vi.mocked(administracaoService.criarEscala).mock.calls[0]!;
    expect(body).toMatchObject({ profissionalId: "p1", vigenciaInicio: "2026-10-05", vigenciaFim: null });
    // Padrão: segunda a sexta, 07h às 17h; sexta sinalizada; sábado e domingo ficam de fora.
    expect(body.faixas).toHaveLength(5);
    expect(body.faixas[0]).toEqual({ dia: "Monday", inicio: "07:00:00", fim: "17:00:00", semAtendimento: false });
    expect(body.faixas[4]).toMatchObject({ dia: "Friday", semAtendimento: true });
    expect(aoConcluir).toHaveBeenCalled();
  });
});

describe("previaEscala", () => {
  it("agrupa_dias_seguidos_com_o_mesmo_horario", () => {
    const f = (dia: "Monday" | "Tuesday" | "Wednesday" | "Friday", fim: string, semAtendimento = false) => ({ dia, inicio: "07:00", fim, semAtendimento });
    expect(previaEscala([f("Monday", "17:00"), f("Tuesday", "17:00"), f("Wednesday", "12:00"), f("Friday", "17:00", true)])).toBe(
      "Seg a Ter 07:00–17:00 · Qua 07:00–12:00 · Sex 07:00–17:00 (sem atendimento)",
    );
    expect(previaEscala([])).toBe("Nenhum dia de atendimento.");
  });
});

// Perfil sem producao.gerir: a seção de autorizações fica oculta e este teste segue só na escala.
const sessaoProfissional: ContextoSessao = {
  estado: "autenticado",
  usuario: { id: "u1", nome: "Ana Souza", email: "a@nap.demo", perfil: "ProfissionalNap", cursoId: null, curso: null, deveTrocarSenha: false },
  saiuPorVontade: false, entrar: vi.fn(), trocarSenha: vi.fn(), sair: vi.fn(),
};

describe("EscalasTab", () => {
  const escala = { id: "e1", profissionalId: "p1", profissional: "Ana Souza", vigenciaInicio: "2026-02-01", vigenciaFim: null, versao: "v-e1", faixas: [{ dia: "Monday" as const, inicio: "07:00:00", fim: "17:00:00", semAtendimento: false }] };

  it("Encerrar_vigencia_envia_o_fim_com_a_versao_lida", async () => {
    vi.mocked(administracaoService.listarEscalas).mockResolvedValue([escala]);
    vi.mocked(administracaoService.vigenciaEscala).mockResolvedValue(undefined);
    render(
      <QueryClientProvider client={cliente()}>
        <SessaoContexto value={sessaoProfissional}>
          <ToastProvider>
            <EscalasTab />
          </ToastProvider>
        </SessaoContexto>
      </QueryClientProvider>,
    );
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Encerrar vigência da escala de Ana Souza" }));
    const d = within(screen.getByRole("dialog"));
    fireEvent.change(d.getByLabelText(/^Último dia de vigência/), { target: { value: "2026-10-31" } });
    await user.click(d.getByRole("button", { name: "Encerrar vigência" }));

    expect(administracaoService.vigenciaEscala).toHaveBeenCalledWith("e1", { vigenciaFim: "2026-10-31" }, "v-e1");
    expect(await screen.findByText("Vigência encerrada.")).toBeInTheDocument();
  });
});
