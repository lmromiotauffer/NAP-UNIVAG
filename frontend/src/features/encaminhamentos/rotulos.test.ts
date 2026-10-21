import { describe, expect, it } from "vitest";
import { diasEntre } from "./rotulos";

describe("diasEntre", () => {
  it("conta_dias_civis_inclusive_na_virada_de_mes_e_de_ano", () => {
    expect(diasEntre("2026-09-28", "2026-10-05")).toBe(7);
    expect(diasEntre("2025-12-30", "2026-01-02")).toBe(3);
    expect(diasEntre("2026-03-01", "2026-03-01")).toBe(0);
  });
  it("e_negativo_quando_o_fim_vem_antes", () => {
    expect(diasEntre("2026-10-05", "2026-10-01")).toBe(-4);
  });
});
