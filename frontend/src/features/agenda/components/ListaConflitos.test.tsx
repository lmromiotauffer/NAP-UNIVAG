// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ListaConflitos } from "./ListaConflitos";

describe("ListaConflitos", () => {
  it("Conflito_mostra_o_profissional", () => {
    render(
      <ListaConflitos
        conflitos={[{ id: "c1", tipo: "Atendimento", inicio: "2026-09-29T12:00:00Z", fim: "2026-09-29T13:00:00Z", aluno: null, profissionalId: "p2", profissional: "Bruno Lima" }]}
      />,
    );
    expect(screen.getByRole("list", { name: "Itens em conflito" })).toHaveTextContent("Bruno Lima");
  });
});
