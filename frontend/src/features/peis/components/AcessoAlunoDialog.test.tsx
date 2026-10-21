// @vitest-environment jsdom
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { peiService } from "../services/peiService";
import { montar } from "../../../test/peis";
import { AcessoAlunoDialog } from "./AcessoAlunoDialog";

vi.mock("../services/peiService", () => ({ peiService: { gerarAcesso: vi.fn() } }));
const gerar = vi.mocked(peiService.gerarAcesso);

beforeEach(() => {
  gerar.mockReset();
  localStorage.clear();
});

describe("AcessoAlunoDialog", () => {
  it("Acesso_mostra_link_e_codigo_uma_vez_e_nao_persiste", async () => {
    gerar.mockResolvedValue({ acessoId: "x", token: "tok123", codigo: "482913", expiraEm: "2026-10-05T10:00:00-04:00" });
    const aoFechar = vi.fn();
    const { unmount } = montar(<AcessoAlunoDialog versaoId="v1" aoFechar={aoFechar} />, "ProfissionalNap");

    expect(gerar).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole("button", { name: "Gerar link e código" }));
    expect(await screen.findByText("482913")).toBeInTheDocument();
    expect(screen.getByText(`${window.location.origin}/assinar/tok123`)).toBeInTheDocument();
    expect(screen.getByText("Entregue o link e o código ao aluno pessoalmente ou por canais separados. Eles aparecem só agora e valem por 72 horas.")).toBeInTheDocument();
    expect(gerar).toHaveBeenCalledTimes(1);
    expect(JSON.stringify({ ...localStorage })).not.toContain("tok123");

    await userEvent.click(screen.getAllByRole("button", { name: "Fechar" }).at(-1)!);
    expect(aoFechar).toHaveBeenCalled();
    unmount();
    // Reabrir gera outro acesso: o anterior não volta de nenhum cache.
    gerar.mockResolvedValue({ acessoId: "y", token: "tok999", codigo: "111111", expiraEm: "2026-10-05T10:00:00-04:00" });
    montar(<AcessoAlunoDialog versaoId="v1" aoFechar={aoFechar} />, "ProfissionalNap");
    expect(screen.queryByText("482913")).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Gerar link e código" }));
    expect(await screen.findByText("111111")).toBeInTheDocument();
  });
});
