// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Button } from "./Button";

describe("Button", () => {
  it("botao_carregando_fica_desabilitado", () => {
    render(<Button carregando>Salvar</Button>);
    const botao = screen.getByRole("button", { name: /salvar/i });
    expect(botao).toBeDisabled();
    expect(botao).toHaveAttribute("aria-busy", "true");
  });
});
