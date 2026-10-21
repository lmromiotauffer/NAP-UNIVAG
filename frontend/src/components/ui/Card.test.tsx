// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Card } from "./Card";

describe("Card", () => {
  it("Card_sem_titulo_so_envolve_o_conteudo_e_aceita_className", () => {
    render(<Card className="p-0">Corpo</Card>);
    expect(screen.queryByRole("heading")).toBeNull();
    expect(screen.getByText("Corpo")).toHaveClass("p-0");
  });
});
