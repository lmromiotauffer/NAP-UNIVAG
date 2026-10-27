// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EmptyState } from "./EmptyState";
import { ErrorState } from "./ErrorState";

// Estado vazio e erro substituem um skeleton já visível: se chegassem com animate-surgir, a área apagaria (opacidade 0)
// e surgiria de novo, parecendo recarregar. Quem anima a chegada é o painel que os contém.
describe("Estados que substituem o skeleton", () => {
  it("vazio_e_erro_nao_tem_animacao_de_entrada", () => {
    render(
      <>
        <EmptyState titulo="Nada aqui" />
        <ErrorState erro={new Error("Falhou")} />
      </>,
    );
    expect(screen.getByText("Nada aqui").parentElement).not.toHaveClass("animate-surgir");
    expect(screen.getByRole("alert")).not.toHaveClass("animate-surgir");
  });
});
