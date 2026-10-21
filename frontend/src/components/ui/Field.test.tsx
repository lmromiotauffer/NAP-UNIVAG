// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Field } from "./Field";
import { Input } from "./Input";

describe("Field", () => {
  it("field_liga_erro_ao_input", () => {
    render(
      <Field rotulo="Nome" erro="Obrigatório" dica="Nome completo">
        <Input />
      </Field>,
    );
    const input = screen.getByLabelText("Nome");
    const erro = screen.getByRole("alert");
    expect(erro).toHaveTextContent("Obrigatório");
    expect(input.getAttribute("aria-describedby")).toContain(erro.id);
    expect(input).toHaveAttribute("aria-invalid", "true");
  });
});
