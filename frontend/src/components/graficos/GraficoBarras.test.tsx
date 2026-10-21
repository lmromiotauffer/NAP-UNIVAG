// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { GraficoBarras } from "./GraficoBarras";

const dados = [
  { rotulo: "ago", valor: 171 },
  { rotulo: "set", valor: null },
  { rotulo: "out", valor: 47, parcial: true },
];

function montar() {
  render(<GraficoBarras titulo="Atendimentos realizados · 6 meses" descricao="Realizados por mês; ausências não contam." dados={dados} unidade="atendimentos" orientacao="vertical" />);
  return screen.getByRole("img", { name: "Atendimentos realizados · 6 meses" });
}

describe("GraficoBarras", () => {
  it("barra_nula_nao_e_desenhada_e_mostra_travessao", () => {
    const grafico = montar();
    expect(grafico.querySelectorAll("[data-barra]")).toHaveLength(2);
    expect(within(grafico).getByText("—")).toBeInTheDocument();
  });

  it("valor_zero_nao_desenha_toco_de_barra", () => {
    render(<GraficoBarras titulo="Zero" descricao="d" dados={[{ rotulo: "jul", valor: 0 }, { rotulo: "ago", valor: 5 }]} unidade="u" orientacao="vertical" />);
    const grafico = screen.getByRole("img", { name: "Zero" });
    expect(grafico.querySelectorAll("[data-barra]")).toHaveLength(1);
    expect(within(grafico).getByText("0")).toBeInTheDocument();
  });

  it("mes_parcial_tem_rotulo_textual", () => {
    const grafico = montar();
    expect(within(grafico).getByText("parcial")).toBeInTheDocument();
    expect(screen.getByRole("row", { name: /out \(parcial\)/ })).toBeInTheDocument();
  });

  it("grafico_tem_nome_e_tabela_acessiveis", () => {
    const grafico = montar();
    expect(grafico.querySelector("desc")).toHaveTextContent("Realizados por mês; ausências não contam.");
    const tabela = screen.getByRole("table", { name: "Atendimentos realizados · 6 meses" });
    const linhas = within(tabela).getAllByRole("row");
    expect(linhas).toHaveLength(4);
    expect(within(tabela).getByRole("row", { name: /ago 171/ })).toBeInTheDocument();
    expect(within(tabela).getByRole("row", { name: /set —/ })).toBeInTheDocument();
  });
});
