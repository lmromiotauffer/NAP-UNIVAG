// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { formatarPercentual } from "../../../lib/format";
import { CartaoIndicador } from "./CartaoIndicador";

describe("CartaoIndicador", () => {
  it("taxa_nula_mostra_travessao_e_motivo", () => {
    render(
      <CartaoIndicador
        titulo="Taxa de ausência"
        valor={null}
        formatar={formatarPercentual}
        base="0 faltas ÷ (0 + 0)"
        motivoNaoCalculado="Sem atendimentos individuais no período"
      />,
    );
    expect(screen.getByText("—")).toBeInTheDocument();
    expect(screen.getByText("Sem atendimentos individuais no período")).toBeInTheDocument();
    expect(screen.queryByText("0%")).not.toBeInTheDocument();
    // Sem valor calculado a base da conta não aparece (não sugere uma taxa de zero).
    expect(screen.queryByText("0 faltas ÷ (0 + 0)")).not.toBeInTheDocument();
  });
});
