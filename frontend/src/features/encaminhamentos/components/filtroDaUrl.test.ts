import { describe, expect, it } from "vitest";
import { filtroDaUrl as filtroAlunos } from "../../alunos/components/FiltrosAlunos";
import { filtroDaUrl } from "./FiltrosEncaminhamentos";

describe("filtroDaUrl", () => {
  it("descarta_dia_inexistente_e_valores_fora_da_lista_num_link_antigo", () => {
    const f = filtroDaUrl(new URLSearchParams("de=2026-13-99&ate=2026-02-30&estado=Xpto&pagina=0"));
    expect(f.de).toBeUndefined();
    expect(f.ate).toBeUndefined();
    expect(f.estado).toBeUndefined();
    expect(f.pagina).toBeUndefined();
  });
  it("mantem_dia_valido_e_pagina_maior_que_um", () => {
    const f = filtroDaUrl(new URLSearchParams("de=2026-02-28&pagina=3"));
    expect(f.de).toBe("2026-02-28");
    expect(f.pagina).toBe(3);
  });
  it("alunos_descarta_dia_inexistente_e_mantem_o_valido", () => {
    const f = filtroAlunos(new URLSearchParams("inicioDe=2026-13-99&inicioAte=2026-03-31"));
    expect(f.inicioDe).toBeUndefined();
    expect(f.inicioAte).toBe("2026-03-31");
  });
});
