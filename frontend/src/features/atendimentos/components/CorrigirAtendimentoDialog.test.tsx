// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AtendimentoDetalhe } from "../../../lib/types";
import { atendimentoService } from "../services/atendimentoService";
import { CorrigirAtendimentoDialog } from "./CorrigirAtendimentoDialog";

vi.mock("../services/atendimentoService", () => ({ atendimentoService: { corrigir: vi.fn() } }));

const corrigir = vi.mocked(atendimentoService.corrigir);

const atendimento: AtendimentoDetalhe = {
  id: "at1",
  compromissoId: null,
  inicio: "2026-09-29T08:00:00-04:00",
  fim: "2026-09-29T09:00:00-04:00",
  alunoId: "a1",
  aluno: "Maria Clara Souza",
  profissionalId: "p1",
  profissional: "Ana Souza",
  tipo: "Acompanhamento",
  estado: "Realizado",
  avulso: true,
  justificativaAvulso: "Procura espontânea",
  dadosClinicos: { demandaPrincipal: "Ansiedade em provas", observacoesPrivadas: null, recomendacoesEncaminhamento: null, proximosPassos: null },
  correcoes: [],
  versao: "v1",
};

function montar(usuarioId: string) {
  const aoConcluir = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient()}>
      <CorrigirAtendimentoDialog atendimento={atendimento} usuarioId={usuarioId} aoConcluir={aoConcluir} aoCancelar={vi.fn()} />
    </QueryClientProvider>,
  );
  return { aoConcluir, user: userEvent.setup() };
}

beforeEach(() => {
  corrigir.mockReset();
});

describe("CorrigirAtendimentoDialog", () => {
  it("coordenacao_corrigindo_outro_profissional_exige_justificativa", async () => {
    const { user } = montar("coord");
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));

    expect(await screen.findByText("Informe a justificativa da correção.")).toBeInTheDocument();
    expect(corrigir).not.toHaveBeenCalled();
  });

  it("proprio_profissional_corrige_sem_justificativa", async () => {
    corrigir.mockResolvedValueOnce(undefined);
    const { user, aoConcluir } = montar("p1");
    expect(screen.queryByLabelText(/Justificativa da correção/)).not.toBeInTheDocument();
    await user.click(screen.getByRole("radio", { name: "Ausente" }));
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));

    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());
    expect(corrigir.mock.calls[0]?.[1]).toMatchObject({ estado: "Ausente", justificativa: null, demandaPrincipal: "Ansiedade em provas" });
  });

  it("envia_a_versao_lida_do_atendimento", async () => {
    corrigir.mockResolvedValueOnce(undefined);
    const { user, aoConcluir } = montar("p1");
    await user.click(screen.getByRole("radio", { name: "Ausente" }));
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));

    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());
    expect(corrigir).toHaveBeenCalledWith("at1", expect.any(Object), "v1");
  });

  it("apos_409_a_retentativa_continua_enviando_a_versao_de_quando_o_dialogo_abriu", async () => {
    corrigir.mockRejectedValueOnce({ status: 409, titulo: "Conflito", errosCampo: {} }).mockResolvedValueOnce(undefined);
    const cliente = new QueryClient();
    const arvore = (a: AtendimentoDetalhe) => (
      <QueryClientProvider client={cliente}>
        <CorrigirAtendimentoDialog atendimento={a} usuarioId="p1" aoConcluir={vi.fn()} aoCancelar={vi.fn()} />
      </QueryClientProvider>
    );
    const { rerender } = render(arvore(atendimento));
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));
    await waitFor(() => expect(corrigir).toHaveBeenCalledTimes(1));

    rerender(arvore({ ...atendimento, versao: "v2" }));
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));
    await waitFor(() => expect(corrigir).toHaveBeenCalledTimes(2));
    expect(corrigir.mock.calls[1]?.[2]).toBe("v1");
  });
});
