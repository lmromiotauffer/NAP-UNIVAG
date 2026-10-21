// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Paginacao } from "./Paginacao";

describe("Paginacao", () => {
  it("pagina_alem_do_fim_volta_para_a_ultima", () => {
    const aoMudar = vi.fn();
    render(<Paginacao pagina={99} tamanho={20} total={30} aoMudar={aoMudar} />);
    expect(aoMudar).toHaveBeenCalledWith(2);
  });

  it("pagina_valida_nao_dispara_mudanca", () => {
    const aoMudar = vi.fn();
    render(<Paginacao pagina={2} tamanho={20} total={30} aoMudar={aoMudar} />);
    expect(screen.getByText("Mostrando 21–30 de 30")).toBeInTheDocument();
    expect(aoMudar).not.toHaveBeenCalled();
  });
});
