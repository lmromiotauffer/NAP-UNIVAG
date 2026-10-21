// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { producao } from "../../../test/producao";
import { GraficoSerie } from "./GraficoSerie";

afterEach(() => vi.restoreAllMocks());

it("semanas do mesmo mês (rótulo repetido) não geram chave duplicada", () => {
  const erro = vi.spyOn(console, "error").mockImplementation(() => {});
  const base = producao();
  const serie = [
    { ...base.serie[0]!, inicio: "2026-09-01", fim: "2026-09-06" },
    { ...base.serie[0]!, inicio: "2026-09-07", fim: "2026-09-13" },
  ];
  render(<GraficoSerie producao={producao({ periodo: { ...base.periodo, tipo: "Semestre" }, serie })} />);
  expect(screen.getAllByRole("img")).toHaveLength(2);
  expect(erro).not.toHaveBeenCalled();
});
