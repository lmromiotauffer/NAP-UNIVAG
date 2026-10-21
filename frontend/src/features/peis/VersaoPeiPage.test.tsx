// @vitest-environment jsdom
import { screen } from "@testing-library/react";
import { Route, Routes } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { peiService } from "./services/peiService";
import { acoesNenhuma, montar, versaoBase } from "../../test/peis";
import { VersaoPeiPage } from "./VersaoPeiPage";

vi.mock("./services/peiService", () => ({ peiService: { obter: vi.fn(), baixarPdf: vi.fn() } }));
const obter = vi.mocked(peiService.obter);

const publicada = { ...versaoBase, estado: "Publicada" as const, acoes: { ...acoesNenhuma, podeRegistrarCiencia: true } };
const pagina = (
  <Routes>
    <Route path="/" element={<VersaoPeiPage />} />
  </Routes>
);

beforeEach(() => obter.mockReset());

describe("VersaoPeiPage", () => {
  it("Coordenador_ve_botao_de_ciencia_e_nao_ve_demanda_interna", async () => {
    obter.mockResolvedValue(publicada);
    montar(pagina, "CoordenadorCurso");
    expect(await screen.findByRole("button", { name: "Registrar ciência" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Demanda interna" })).not.toBeInTheDocument();
  });

  it("Equipe_nap_ve_demanda_interna_mesmo_vazia", async () => {
    obter.mockResolvedValue({ ...publicada, acoes: acoesNenhuma, conteudo: { ...publicada.conteudo, demandaInterna: "" } });
    montar(pagina, "ProfissionalNap");
    expect(await screen.findByRole("heading", { name: "Demanda interna" })).toBeInTheDocument();
    expect(screen.getByText("Não preenchida.")).toBeInTheDocument();
  });

  it("Mostra_selo_de_vigencia_encerrada", async () => {
    obter.mockResolvedValue({ ...publicada, foraDaVigencia: true });
    montar(pagina, "ProfissionalNap");
    expect(await screen.findByText("Vigência encerrada")).toBeInTheDocument();
  });
});
