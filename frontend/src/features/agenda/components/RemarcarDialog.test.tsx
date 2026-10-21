// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { Compromisso } from "../../../lib/types";
import { agendaService } from "../services/agendaService";
import { RemarcarDialog } from "./RemarcarDialog";

vi.mock("../services/agendaService", () => ({ agendaService: { remarcar: vi.fn() } }));

const bloqueio: Compromisso = {
  id: "b1",
  tipo: "Bloqueio",
  titulo: "Bloqueio",
  inicio: "2026-09-29T08:00:00-04:00",
  fim: "2026-09-29T09:00:00-04:00",
  profissionalId: "p1",
  profissional: "Ana Souza",
  alunoId: null,
  aluno: null,
  estado: "Agendado",
  estadoAtendimento: null,
  remarcadoDeId: null,
  descricao: null,
  versao: "v1",
  grupoId: null,
  participantes: null,
  podeGerirGrupo: false,
};

describe("RemarcarDialog", () => {
  it("Remarcar_bloqueio_em_conflito_mostra_a_lista", async () => {
    vi.mocked(agendaService.remarcar).mockRejectedValueOnce({
      status: 409,
      titulo: "Conflito de horário",
      errosCampo: {},
      conflitos: [{ id: "c1", tipo: "Atividade", inicio: "2026-09-29T12:00:00Z", fim: "2026-09-29T13:00:00Z", aluno: null }],
    });
    const aoConcluir = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient()}>
        <RemarcarDialog compromisso={bloqueio} aoConcluir={aoConcluir} aoCancelar={vi.fn()} />
      </QueryClientProvider>,
    );
    await userEvent.setup().click(screen.getByRole("button", { name: "Remarcar" }));

    const lista = await screen.findByRole("list", { name: "Itens em conflito" });
    expect(lista).toHaveTextContent("Atividade");
    expect(aoConcluir).not.toHaveBeenCalled();
  });
});
