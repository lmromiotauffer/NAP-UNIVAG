import { hashKey } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { comParametro, INVALIDAR_APOS_ALUNO, INVALIDAR_APOS_ATENDIMENTO, paginaDaUrl, queryKeys } from "./queryKeys";

describe("paginaDaUrl", () => {
  it("so_aceita_inteiro_maior_que_um", () => {
    expect(paginaDaUrl(new URLSearchParams("pagina=3"))).toBe(3);
    for (const p of ["", "pagina=1", "pagina=0", "pagina=-2", "pagina=abc", "pagina=2.5"]) expect(paginaDaUrl(new URLSearchParams(p))).toBeUndefined();
  });
});

describe("comParametro", () => {
  it("mudar_um_filtro_volta_a_primeira_pagina_e_valor_vazio_remove", () => {
    const atual = new URLSearchParams("busca=ana&pagina=3");
    expect(comParametro(atual, "cursoId", "x").toString()).toBe("busca=ana&cursoId=x");
    expect(comParametro(atual, "busca", null).toString()).toBe("");
    expect(atual.toString()).toBe("busca=ana&pagina=3");
  });
  it("pagina_e_id_nao_zeram_a_pagina", () => {
    expect(comParametro(new URLSearchParams("busca=ana&pagina=3"), "pagina", "4").toString()).toBe("busca=ana&pagina=4");
    expect(comParametro(new URLSearchParams("pagina=3"), "id", "abc").toString()).toBe("pagina=3&id=abc");
  });
});

describe("queryKeys", () => {
  it("chaves_equivalentes_sao_iguais_independente_da_ordem", () => {
    expect(hashKey(queryKeys.alunos.lista({ b: 2, a: 1 }))).toBe(hashKey(queryKeys.alunos.lista({ a: 1, b: 2, c: "", d: undefined })));
  });
  it("prefixos_cobrem_chaves_especificas", () => {
    expect(INVALIDAR_APOS_ATENDIMENTO.map((p) => p[0])).toContain(queryKeys.alunos.ficha("1")[0]);
  });
  it("aluno_alterado_invalida_onde_o_nome_aparece", () => {
    expect(INVALIDAR_APOS_ALUNO.map((p) => p[0])).toEqual(
      expect.arrayContaining([
        queryKeys.agenda.escalaSemana("2026-09-28")[0],
        queryKeys.atendimentos.detalhe("1")[0],
        queryKeys.encaminhamentos.lista()[0],
        queryKeys.comunicacoes.caixa()[0],
        queryKeys.peis.versao("1")[0],
      ]),
    );
  });
});
