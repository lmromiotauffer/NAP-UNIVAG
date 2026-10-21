// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AtividadeDetalhe } from "../../../lib/types";
import { responderApi } from "../../../test/apiFalsa";
import { atividadeService } from "../services/atividadeService";
import { CorrigirAtividadeDialog } from "./CorrigirAtividadeDialog";

vi.mock("../services/atividadeService", () => ({ atividadeService: { corrigir: vi.fn() } }));

const corrigir = vi.mocked(atividadeService.corrigir);

const atividade: AtividadeDetalhe = {
  id: "at1",
  data: "2026-09-29",
  turno: "Manha",
  inicio: "2026-09-29T08:00:00-04:00",
  fim: "2026-09-29T09:00:00-04:00",
  duracaoMinutos: 60,
  cursoId: "cu1",
  curso: "Direito",
  profissionalId: "p1",
  profissional: "Ana Souza",
  tipo: "RodaConversa",
  descricao: "Acolhimento dos calouros",
  avulsa: false,
  versao: "v1",
  justificativaAvulso: null,
  compromissoId: "c1",
  correcoes: [],
  podeCorrigir: true,
};

function arvore(a: AtividadeDetalhe, cliente: QueryClient, aoConcluir = vi.fn()) {
  return (
    <QueryClientProvider client={cliente}>
      <CorrigirAtividadeDialog atividade={a} aoConcluir={aoConcluir} aoCancelar={vi.fn()} />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  corrigir.mockReset();
  responderApi({ "/cursos": [{ id: "cu1", nome: "Direito", ativo: true, versao: "v" }] });
});

describe("CorrigirAtividadeDialog", () => {
  it("Correcao_exige_justificativa_e_envia_a_versao_aberta", async () => {
    corrigir.mockResolvedValueOnce(undefined);
    const aoConcluir = vi.fn();
    render(arvore(atividade, new QueryClient(), aoConcluir));
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));
    expect(await screen.findByText("Informe a justificativa da correção.")).toBeInTheDocument();
    expect(corrigir).not.toHaveBeenCalled();

    await user.selectOptions(screen.getByLabelText(/Tipo/), "ReuniaoPedagogica");
    await user.type(screen.getByLabelText(/Justificativa da correção/), "Tipo registrado errado");
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));

    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());
    expect(corrigir).toHaveBeenCalledWith(
      "at1",
      {
        inicio: "2026-09-29T08:00:00-04:00",
        fim: "2026-09-29T09:00:00-04:00",
        cursoId: "cu1",
        tipo: "ReuniaoPedagogica",
        descricao: "Acolhimento dos calouros",
        justificativa: "Tipo registrado errado",
      },
      "v1",
    );
  });

  it("apos_409_mostra_a_mensagem_e_a_retentativa_continua_com_a_versao_de_quando_abriu", async () => {
    corrigir.mockRejectedValueOnce({ status: 409, titulo: "Conflito", detalhe: "A atividade foi alterada por outra pessoa.", errosCampo: {} }).mockResolvedValueOnce(undefined);
    const cliente = new QueryClient();
    const { rerender } = render(arvore(atividade, cliente));
    const user = userEvent.setup();
    await user.type(screen.getByLabelText(/Justificativa da correção/), "Horário errado");
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));
    expect(await screen.findByText("A atividade foi alterada por outra pessoa.")).toBeInTheDocument();

    rerender(arvore({ ...atividade, versao: "v2" }, cliente));
    await user.click(screen.getByRole("button", { name: "Salvar correção" }));
    await waitFor(() => expect(corrigir).toHaveBeenCalledTimes(2));
    expect(corrigir.mock.calls[1]?.[2]).toBe("v1");
  });

  it("curso_inativo_da_atividade_continua_na_lista_e_selecionado", async () => {
    responderApi({ "/cursos": [{ id: "cu1", nome: "Direito", ativo: false, versao: "v" }] });
    render(arvore(atividade, new QueryClient()));
    const opcao = await screen.findByRole("option", { name: "Direito" });
    expect(opcao).toBeInTheDocument();
    expect(screen.getByLabelText(/Curso/)).toHaveValue("cu1");
  });
});
