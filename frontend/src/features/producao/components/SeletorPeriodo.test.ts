import { describe, expect, it } from "vitest";
import { deslocarReferencia } from "./SeletorPeriodo";

describe("deslocarReferencia", () => {
  it("semana_anda_sete_dias", () => {
    expect(deslocarReferencia("Semana", "2026-09-30", 1)).toBe("2026-10-07");
    expect(deslocarReferencia("Semana", "2026-01-03", -1)).toBe("2025-12-27");
  });
  it("mes_vira_o_ano_nos_dois_sentidos_e_cai_no_dia_1", () => {
    expect(deslocarReferencia("Mes", "2026-12-31", 1)).toBe("2027-01-01");
    expect(deslocarReferencia("Mes", "2026-01-15", -1)).toBe("2025-12-01");
  });
  it("semestre_e_ano_deslocam_pelo_numero_de_meses", () => {
    expect(deslocarReferencia("Semestre", "2026-08-10", 1)).toBe("2027-02-01");
    expect(deslocarReferencia("Semestre", "2026-03-10", -1)).toBe("2025-09-01");
    expect(deslocarReferencia("Ano", "2026-06-10", -1)).toBe("2025-06-01");
  });
});
