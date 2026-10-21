import { describe, expect, it } from "vitest";
import type { ErroApi } from "./lib/api";
import { deveRetentar } from "./queryClient";

const erro = (status: number): ErroApi => ({ status, titulo: "x", errosCampo: {} });

describe("deveRetentar", () => {
  it("uma_vez_so_para_rede_ou_servidor", () => {
    expect(deveRetentar(0, erro(0))).toBe(true);
    expect(deveRetentar(0, erro(503))).toBe(true);
    expect(deveRetentar(1, erro(503))).toBe(false);
    expect(deveRetentar(0, erro(404))).toBe(false);
    expect(deveRetentar(0, erro(403))).toBe(false);
  });
});
