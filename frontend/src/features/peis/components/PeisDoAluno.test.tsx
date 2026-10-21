// @vitest-environment jsdom
import { screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { peiService } from "../services/peiService";
import { montar } from "../../../test/peis";
import { PeisDoAluno } from "./PeisDoAluno";

vi.mock("../services/peiService", () => ({ peiService: { listarDoAluno: vi.fn() } }));
vi.mock("../../compartilhado/catalogos", () => ({ useProfissionais: () => ({ data: [] }) }));
const listar = vi.mocked(peiService.listarDoAluno);

beforeEach(() => listar.mockReset());

describe("PeisDoAluno", () => {
  it("Lista_versoes_com_estado_em_texto_e_link", async () => {
    listar.mockResolvedValue([
      { id: "p1", responsavel: "Ana Beatriz", versoes: [{ id: "v1", numero: 1, estado: "Publicada", vigenciaInicio: "2026-02-01", vigenciaFim: "2026-07-31", publicadaEm: "2026-03-01T10:00:00-04:00", cienciaRegistradaPorMim: true, foraDaVigencia: false }] },
    ]);
    montar(<PeisDoAluno alunoId="a1" />, "CoordenadorCurso");
    expect(await screen.findByRole("link", { name: "Versão 1" })).toHaveAttribute("href", "/peis/versoes/v1");
    expect(screen.getByText("Publicada")).toBeInTheDocument();
    expect(screen.getByText("Ciência registrada")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Criar PEI" })).not.toBeInTheDocument();
  });

  it("Equipe_nap_cria_pei_quando_nao_existe", async () => {
    listar.mockResolvedValue([]);
    montar(<PeisDoAluno alunoId="a1" />, "ProfissionalNap");
    expect(await screen.findByRole("button", { name: "Criar PEI" })).toBeInTheDocument();
  });

  it("Mostra_selo_de_vigencia_encerrada", async () => {
    listar.mockResolvedValue([
      { id: "p1", responsavel: "Ana Beatriz", versoes: [{ id: "v1", numero: 1, estado: "Publicada", vigenciaInicio: "2026-02-01", vigenciaFim: "2026-07-31", publicadaEm: null, cienciaRegistradaPorMim: false, foraDaVigencia: true }] },
    ]);
    montar(<PeisDoAluno alunoId="a1" />, "CoordenadorCurso");
    expect(await screen.findByText("Vigência encerrada")).toBeInTheDocument();
  });
});
