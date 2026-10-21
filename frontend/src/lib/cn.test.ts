import { describe, expect, it } from "vitest";
import { cn } from "./cn";

describe("cn", () => {
  it("tamanho_de_token_nao_conflita_com_cor", () => {
    expect(cn("text-denso text-suave")).toBe("text-denso text-suave");
    expect(cn("text-denso", "text-rotulo")).toBe("text-rotulo");
    expect(cn("rounded-md", "rounded-campo")).toBe("rounded-campo");
  });
});
