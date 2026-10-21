import type { CategoriaProducao, Producao } from "../lib/types";

export const categoria = (chave: CategoriaProducao["chave"], rotulo: string, oc: number | null, min: number, ajOc: number | null = oc == null ? null : 0, ajMin = 0): CategoriaProducao => ({
  chave,
  rotulo,
  registrado: { ocorrencias: oc, minutos: min },
  ajustes: { ocorrencias: ajOc, minutos: ajMin },
  total: { ocorrencias: oc == null ? null : oc + (ajOc ?? 0), minutos: min + ajMin },
});

export function producao(extra: Partial<Producao> = {}): Producao {
  const categorias = [
    categoria("AtendimentosRealizados", "Atendimentos realizados", 22, 1320),
    categoria("Ausencias", "Ausências de alunos", 3, 180),
    categoria("Ociosidade", "Horários disponíveis sem utilização", null, 240),
  ];
  return {
    periodo: { tipo: "Mes", inicio: "2026-09-01", fim: "2026-09-30", parcial: false },
    categorias,
    tempo: { capacidade: 1800, producaoNaEscala: 1320, ausenciasNaEscala: 180, pendentesNaEscala: 60, ociosidade: 240, tempoExtra: 0, inconsistente: false, diasInconsistentes: [] },
    taxaAusencia: { valor: 0.12, numerador: 3, denominador: 25, motivo: null },
    pendentes: 1,
    serie: [
      { inicio: "2026-09-01", fim: "2026-09-06", parcial: false, categorias },
      { inicio: "2026-09-07", fim: "2026-09-13", parcial: true, categorias },
    ],
    anterior: {
      periodo: { tipo: "Mes", inicio: "2026-08-01", fim: "2026-08-31", parcial: false },
      categorias: [{ chave: "AtendimentosRealizados", anterior: { ocorrencias: 0, minutos: 0 }, diferenca: { ocorrencias: 22, minutos: 1320 }, percentualOcorrencias: null, percentualMinutos: null }],
    },
    porProfissional: null,
    ...extra,
  };
}
