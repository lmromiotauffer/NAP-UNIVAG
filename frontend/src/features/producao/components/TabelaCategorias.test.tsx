// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { producao } from "../../../test/producao";
import { TabelaCategorias } from "./TabelaCategorias";

describe("TabelaCategorias", () => {
  it("Ocorrencias_e_tempo_em_colunas_separadas", () => {
    render(<TabelaCategorias categorias={producao().categorias} />);
    expect(screen.getByRole("columnheader", { name: "Ocorrências" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Tempo" })).toBeInTheDocument();
    const atendimentos = within(screen.getByRole("row", { name: /Atendimentos realizados/ })).getAllByRole("cell");
    expect(atendimentos.map((c) => c.textContent)).toEqual(["22", "0", "22", "22h00", "0 min", "22h00"]);
  });

  it("Ociosidade_sem_ocorrencias_mostra_traco", () => {
    render(<TabelaCategorias categorias={producao().categorias} />);
    const ociosidade = within(screen.getByRole("row", { name: /Horários disponíveis/ })).getAllByRole("cell");
    expect(ociosidade.slice(0, 3).map((c) => c.textContent)).toEqual(["—", "—", "—"]);
    expect(ociosidade[5]?.textContent).toBe("4h00");
  });
});
