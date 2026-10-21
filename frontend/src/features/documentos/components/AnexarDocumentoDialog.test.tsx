// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ErroApi } from "../../../lib/api";
import { documentoService } from "../services/documentoService";
import { AnexarDocumentoDialog } from "./AnexarDocumentoDialog";

vi.mock("../services/documentoService", () => ({ documentoService: { anexar: vi.fn() } }));

const anexar = vi.mocked(documentoService.anexar);

function montar() {
  const aoConcluir = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient()}>
      <AnexarDocumentoDialog alunoId="a1" aoFechar={vi.fn()} aoConcluir={aoConcluir} />
    </QueryClientProvider>,
  );
  return { aoConcluir, user: userEvent.setup() };
}

async function preencher(user: ReturnType<typeof userEvent.setup>, arquivo: File) {
  await user.upload(screen.getByLabelText(/Arquivo/), arquivo);
  await user.selectOptions(screen.getByLabelText(/Categoria/), "Laudo");
  await user.type(screen.getByLabelText(/Descrição/), "Laudo de 2026");
}

beforeEach(() => anexar.mockReset());

describe("AnexarDocumentoDialog", () => {
  it("Mostra_o_aviso_de_restricao_e_aceita_so_as_extensoes_da_api", () => {
    montar();
    expect(screen.getByText("Documentos nascem restritos ao NAP. Compartilhar com a coordenação é uma ação separada.")).toBeInTheDocument();
    expect(screen.getByLabelText(/Arquivo/)).toHaveAttribute("accept", ".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.txt,.csv");
  });

  it("Envia_formdata_com_a_mesma_chave_de_idempotencia_nas_retentativas", async () => {
    anexar.mockRejectedValueOnce({ status: 500, titulo: "Erro interno do servidor", errosCampo: {} } satisfies ErroApi).mockResolvedValueOnce({ id: "d1" });
    const { user, aoConcluir } = montar();
    await preencher(user, new File(["%PDF-"], "laudo.pdf", { type: "application/pdf" }));
    await user.click(screen.getByRole("button", { name: "Anexar" }));
    expect(await screen.findByText("Erro interno do servidor")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Anexar" }));
    await waitFor(() => expect(aoConcluir).toHaveBeenCalled());

    const [alunoId, form, chave] = anexar.mock.calls[1] as [string, FormData, string];
    expect(alunoId).toBe("a1");
    expect((form.get("arquivo") as File).name).toBe("laudo.pdf");
    expect(form.get("categoria")).toBe("Laudo");
    expect(form.get("descricao")).toBe("Laudo de 2026");
    expect(anexar.mock.calls[0]?.[2]).toBe(chave);
  });

  it("Erro_de_formato_aparece_no_campo_arquivo", async () => {
    anexar.mockRejectedValueOnce({ status: 400, titulo: "Requisição inválida", errosCampo: { arquivo: ["O conteúdo não corresponde ao formato .pdf."] } } satisfies ErroApi);
    const { user, aoConcluir } = montar();
    await preencher(user, new File(["MZ"], "falso.pdf", { type: "application/pdf" }));
    await user.click(screen.getByRole("button", { name: "Anexar" }));
    expect(await screen.findByText("O conteúdo não corresponde ao formato .pdf.")).toBeInTheDocument();
    expect(screen.getByLabelText(/Arquivo/)).toHaveAttribute("aria-invalid", "true");
    expect(aoConcluir).not.toHaveBeenCalled();
  });
});
