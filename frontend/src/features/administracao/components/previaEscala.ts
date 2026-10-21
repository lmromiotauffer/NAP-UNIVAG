import { DIAS_ESCALA } from "../../../lib/format";
import type { FaixaEscala } from "../../../lib/types";

type FaixaTexto = { dia: FaixaEscala["dia"]; inicio: string; fim: string; semAtendimento: boolean };

/** "Seg a Qui 07:00–17:00 · Sex 07:00–17:00 (sem atendimento)": agrupa dias seguidos com o mesmo horário. */
export function previaEscala(faixas: FaixaTexto[]): string {
  const grupos: { dias: string[]; texto: string }[] = [];
  for (const { dia, curto } of DIAS_ESCALA) {
    const f = faixas.find((x) => x.dia === dia);
    if (!f) continue;
    const texto = `${f.inicio.slice(0, 5)}–${f.fim.slice(0, 5)}${f.semAtendimento ? " (sem atendimento)" : ""}`;
    const ultimo = grupos.at(-1);
    // Só junta quando o dia anterior da semana também está no grupo (sem buraco no meio).
    if (ultimo?.texto === texto && ultimo.dias.at(-1) === DIAS_ESCALA[DIAS_ESCALA.findIndex((d) => d.dia === dia) - 1]?.curto) ultimo.dias.push(curto);
    else grupos.push({ dias: [curto], texto });
  }
  if (grupos.length === 0) return "Nenhum dia de atendimento.";
  return grupos.map((g) => `${g.dias.length > 1 ? `${g.dias[0]} a ${g.dias.at(-1)}` : g.dias[0]} ${g.texto}`).join(" · ");
}
