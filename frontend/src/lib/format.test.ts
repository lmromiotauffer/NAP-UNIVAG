import { describe, expect, it } from "vitest";
import { diasEntre, diaValido, formatarDataCivil, formatarDataLonga, formatarDiaSemana, formatarSemana, formatarTamanho, hojeCivil, horaCivil, instanteCivil, minutosDoDia, saudacao, segundaDoDia, somarDias } from "./format";
import {
  diaCivil,
  formatarData,
  formatarDataHora,
  formatarDuracao,
  formatarHora,
  formatarInteiro,
  formatarPercentual,
} from "./format";

describe("format", () => {
  it("null_vira_travessao", () => {
    const funcoes = [
      formatarData,
      formatarHora,
      formatarDataHora,
      formatarDuracao,
      formatarInteiro,
      formatarPercentual,
    ];
    for (const f of funcoes) {
      expect(f(null)).toBe("—");
      expect(f(undefined as never)).toBe("—");
    }
  });

  it("percentual_de_fracao", () => {
    expect(formatarPercentual(0.14)).toBe("14%");
    expect(formatarPercentual(0.1234, 1)).toBe("12,3%");
    // Variação negativa que arredonda a zero não ganha sinal.
    expect(formatarPercentual(-0.004)).toBe("0%");
    expect(formatarPercentual(-0.2)).toBe("-20%");
  });

  it("duracao_em_horas_e_minutos", () => {
    expect(formatarDuracao(2295)).toBe("38h15");
    expect(formatarDuracao(783000)).toBe("13.050h00");
    expect(formatarDuracao(50)).toBe("50 min");
    expect(formatarDuracao(0)).toBe("0 min");
  });

  it("hora_no_fuso_institucional", () => {
    expect(formatarHora("2026-10-01T14:12:00Z")).toBe("10h12");
  });

  it("datas_no_fuso_institucional", () => {
    expect(formatarData("2026-10-01T14:12:00Z")).toBe("01/10/2026");
    expect(formatarData("2026-10-01T02:00:00Z")).toBe("30/09/2026");
    expect(formatarDataHora("2026-10-01T14:12:00Z")).toBe("01/10/2026 10h12");
    expect(diaCivil("2026-10-01T02:00:00Z")).toBe("2026-09-30");
  });

  it("data_civil_sem_deslocar_o_dia", () => {
    expect(formatarDataCivil("2026-03-01")).toBe("01/03/2026");
    expect(formatarDataCivil(null)).toBe("—");
  });

  it("numeros_em_pt_br", () => {
    expect(formatarInteiro(12345)).toBe("12.345");
  });
});

describe("saudação e data longa (America/Cuiaba)", () => {
  it("saudacao_pela_hora_local_de_cuiaba", () => {
    // Cuiabá = UTC-4.
    expect(saudacao(new Date("2026-10-01T15:59:00Z"))).toBe("Bom dia");
    expect(saudacao(new Date("2026-10-01T16:00:00Z"))).toBe("Boa tarde");
    expect(saudacao(new Date("2026-10-01T21:59:00Z"))).toBe("Boa tarde");
    expect(saudacao(new Date("2026-10-01T22:00:00Z"))).toBe("Boa noite");
    expect(saudacao(new Date("2026-10-02T03:30:00Z"))).toBe("Boa noite");
  });

  it("data_longa_sem_feira", () => {
    expect(formatarDataLonga(new Date("2026-10-01T14:12:00Z"))).toBe("quinta, 1 de outubro de 2026");
  });
});

describe("agenda: semana civil em Cuiabá", () => {
  it("hoje_civil_usa_o_dia_de_cuiaba", () => {
    // 02h UTC de 2/10 ainda é 1/10 em Cuiabá (UTC-4).
    expect(hojeCivil(new Date("2026-10-02T02:00:00Z"))).toBe("2026-10-01");
    expect(hojeCivil(new Date("2026-10-02T05:00:00Z"))).toBe("2026-10-02");
  });

  it("segunda_do_dia_civil", () => {
    expect(segundaDoDia("2026-10-01")).toBe("2026-09-28");
    expect(segundaDoDia("2026-09-28")).toBe("2026-09-28");
    expect(segundaDoDia("2026-10-04")).toBe("2026-09-28");
    expect(segundaDoDia("2026-10-05")).toBe("2026-10-05");
  });

  it("somar_dias_atravessa_mes_e_ano", () => {
    expect(somarDias("2026-09-28", 4)).toBe("2026-10-02");
    expect(somarDias("2026-09-28", -7)).toBe("2026-09-21");
    expect(somarDias("2026-12-28", 7)).toBe("2027-01-04");
  });

  it("hora_civil_em_cuiaba", () => {
    expect(horaCivil("2026-10-01T14:05:00Z")).toBe("10:05");
    expect(horaCivil("2026-10-01T07:00:00-04:00")).toBe("07:00");
  });

  it("instante_civil_com_offset_de_cuiaba", () => {
    expect(instanteCivil("2026-10-01", "10:00")).toBe("2026-10-01T10:00:00-04:00");
    expect(new Date(instanteCivil("2026-10-01", "10:00")).toISOString()).toBe("2026-10-01T14:00:00.000Z");
  });

  it("rotulo_da_semana", () => {
    expect(formatarSemana("2026-09-28")).toBe("28 set – 2 out 2026");
    expect(formatarSemana("2026-10-05")).toBe("5 – 9 out 2026");
    expect(formatarSemana("2025-12-29")).toBe("29 dez 2025 – 2 jan 2026");
    expect(formatarSemana("2026-10-05", 6)).toBe("5 – 11 out 2026");
  });

  it("rotulo_do_dia_da_semana", () => {
    expect(formatarDiaSemana("2026-09-28")).toBe("Seg 28");
    expect(formatarDiaSemana("2026-10-02")).toBe("Sex 2");
  });
});

describe("formatarTamanho", () => {
  it("usa_unidades_de_1024_em_pt_br", () => {
    expect(formatarTamanho(512)).toBe("512 B");
    expect(formatarTamanho(1024)).toBe("1 KB");
    expect(formatarTamanho(1258291)).toBe("1,2 MB");
    expect(formatarTamanho(5242880)).toBe("5 MB");
    expect(formatarTamanho(null)).toBe("—");
  });
});

describe("diaValido e minutosDoDia", () => {
  it("Dia_inexistente_ou_mal_formado_e_invalido", () => {
    expect(diaValido("2026-02-28")).toBe(true);
    expect(diaValido("2026-02-30")).toBe(false);
    expect(diaValido("2026-2-1")).toBe(false);
    expect(diaValido("")).toBe(false);
  });

  it("Hora_com_ou_sem_segundos_vira_minutos_do_dia", () => {
    expect(minutosDoDia("07:30")).toBe(450);
    expect(minutosDoDia("17:00:00")).toBe(1020);
  });
});

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
