import { describe, expect, it } from "vitest";

// tokens.css zera a paleta padrão (`--color-*: initial`): `text-white`, `bg-red-500` etc. não geram CSS e falham em silêncio.
const PALETA_PADRAO =
  /(?<![\w-])(?:text|bg|border|ring|fill|stroke|outline|from|via|to|divide|accent|decoration|shadow|caret|placeholder)-(?:white|black|(?:slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-\d)/g;

const fontes = import.meta.glob<string>("../**/*.tsx", { query: "?raw", import: "default", eager: true });

describe("cores", () => {
  it("nenhum_componente_usa_a_paleta_padrao_do_tailwind", () => {
    const achados = Object.entries(fontes).flatMap(([arquivo, codigo]) => [...codigo.matchAll(PALETA_PADRAO)].map((m) => `${arquivo}: ${m[0]}`));
    expect(Object.keys(fontes).length).toBeGreaterThan(50);
    expect(achados).toEqual([]);
  });
});
