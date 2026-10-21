// @vitest-environment jsdom
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { responderApi } from "../../../test/apiFalsa";
import { peiService } from "../services/peiService";
import { acoesNenhuma, montar, versaoBase } from "../../../test/peis";
import { AcoesVersao } from "./AcoesVersao";

vi.mock("../services/peiService", () => ({ peiService: { baixarPdf: vi.fn(), trocarResponsavel: vi.fn(), aprovar: vi.fn() } }));
responderApi({ "/usuarios/profissionais": [{ id: "prof2", nome: "Bruno Lima", perfil: "ProfissionalNap" }] });

const botao = (nome: string) => screen.queryByRole("button", { name: nome });

describe("AcoesVersao", () => {
  it("Acoes_seguem_o_dto", () => {
    montar(<AcoesVersao versao={{ ...versaoBase, estado: "EmRevisao", acoes: { ...acoesNenhuma, podeAprovar: true, podeDevolver: true } }} />, "CoordenadorNap");
    expect(botao("Aprovar revisão")).toBeInTheDocument();
    expect(botao("Devolver para ajustes")).toBeInTheDocument();
    expect(botao("Publicar")).not.toBeInTheDocument();
    expect(botao("Gerar acesso do aluno")).not.toBeInTheDocument();
    expect(botao("Registrar ciência")).not.toBeInTheDocument();
    expect(botao("Baixar PDF")).toBeInTheDocument();
  });

  it("Spinner_so_no_botao_da_acao_em_voo", async () => {
    vi.mocked(peiService.aprovar).mockReturnValue(new Promise(() => {}));
    montar(<AcoesVersao versao={{ ...versaoBase, estado: "EmRevisao", acoes: { ...acoesNenhuma, podeAprovar: true, podeDevolver: true } }} />, "CoordenadorNap");
    await userEvent.setup().click(screen.getByRole("button", { name: "Aprovar revisão" }));
    await waitFor(() => expect(botao("Aprovar revisão")).toHaveAttribute("aria-busy", "true"));
    expect(botao("Devolver para ajustes")).toBeDisabled();
    expect(botao("Devolver para ajustes")).not.toHaveAttribute("aria-busy");
  });

  it("Descartar_rascunho_so_na_versao_posterior_a_1_para_equipe_nap", () => {
    const { unmount } = montar(<AcoesVersao versao={{ ...versaoBase, numero: 2 }} />, "ProfissionalNap");
    expect(botao("Descartar rascunho")).toBeInTheDocument();
    unmount();
    montar(<AcoesVersao versao={versaoBase} />, "ProfissionalNap");
    expect(botao("Descartar rascunho")).not.toBeInTheDocument();
  });

  it("Coordenador_ve_botao_de_ciencia", () => {
    montar(<AcoesVersao versao={{ ...versaoBase, estado: "Publicada", acoes: { ...acoesNenhuma, podeRegistrarCiencia: true } }} />, "CoordenadorCurso");
    expect(botao("Registrar ciência")).toBeInTheDocument();
    expect(screen.getByText("Registrar ciência confirma que a coordenação leu esta versão. Não é assinatura.")).toBeInTheDocument();
    expect(botao("Descartar rascunho")).not.toBeInTheDocument();
  });

  it("Trocar_responsavel_so_para_quem_publica_e_envia_justificativa", async () => {
    const { unmount } = montar(<AcoesVersao versao={versaoBase} />, "ProfissionalNap");
    expect(botao("Trocar responsável")).not.toBeInTheDocument();
    unmount();

    vi.mocked(peiService.trocarResponsavel).mockResolvedValue(undefined);
    montar(<AcoesVersao versao={versaoBase} />, "CoordenadorNap");
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Trocar responsável" }));
    const d = within(screen.getByRole("dialog"));
    await user.click(d.getByRole("button", { name: "Trocar responsável" }));
    expect(await d.findByText("Escolha o novo responsável.")).toBeInTheDocument();
    expect(peiService.trocarResponsavel).not.toHaveBeenCalled();

    await user.selectOptions(await d.findByLabelText(/Novo responsável/), "prof2");
    await user.type(d.getByLabelText(/Justificativa/), "Afastamento da titular");
    await user.click(d.getByRole("button", { name: "Trocar responsável" }));
    expect(peiService.trocarResponsavel).toHaveBeenCalledWith("p1", { novoResponsavelId: "prof2", justificativa: "Afastamento da titular" });
  });
});
