import { describe, expect, it } from "vitest";
import { destinoSeguro } from "./navegacao";

describe("destinoSeguro", () => {
  it("voltar_so_aceita_caminho_relativo_da_propria_origem", () => {
    expect(destinoSeguro("/alunos/1?aba=resumo")).toBe("/alunos/1?aba=resumo");
    for (const ruim of [null, "", "https://evil.com", "//evil.com", "/\\evil.com", "/\t/evil.com", "alunos", "/entrar?voltar=/"]) {
      expect(destinoSeguro(ruim)).toBe("/");
    }
  });
});
