// @vitest-environment jsdom
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { peiService } from "../services/peiService";
import { acoesNenhuma, montar, versaoBase } from "../../../test/peis";
import { ConteudoPeiForm } from "./ConteudoPeiForm";

vi.mock("../services/peiService", () => ({ peiService: { salvarConteudo: vi.fn(), enviarRevisao: vi.fn(), obter: vi.fn() } }));
const salvar = vi.mocked(peiService.salvarConteudo);
const enviar = vi.mocked(peiService.enviarRevisao);
const obter = vi.mocked(peiService.obter);

const versao = { ...versaoBase, acoes: { ...acoesNenhuma, podeEditar: true, podeEnviarRevisao: true } };

beforeEach(() => {
  salvar.mockReset().mockResolvedValue("e2");
  enviar.mockReset().mockResolvedValue(undefined);
  // Releitura daria a versão de outra aba: o formulário não pode usá-la.
  obter.mockReset().mockResolvedValue({ ...versao, versao: "outra-aba" });
});

describe("ConteudoPeiForm", () => {
  it("Formulario_exige_vigencia_e_conteudo_pedagogico_para_enviar", async () => {
    montar(<ConteudoPeiForm versao={versao} />, "ProfissionalNap");
    await userEvent.click(screen.getByRole("button", { name: "Enviar para revisão" }));
    expect(await screen.findByText(/Preencha ao menos um dos campos de conteúdo pedagógico/)).toBeInTheDocument();
    expect(salvar).not.toHaveBeenCalled();

    await userEvent.clear(screen.getByLabelText(/Fim da vigência/));
    await userEvent.click(screen.getByRole("button", { name: "Salvar rascunho" }));
    expect(await screen.findByText("Informe o fim da vigência.")).toBeInTheDocument();
    expect(salvar).not.toHaveBeenCalled();
  });

  it("Enviar_grava_o_conteudo_e_envia_com_a_versao_nova", async () => {
    montar(<ConteudoPeiForm versao={versao} />, "ProfissionalNap");
    await userEvent.type(screen.getByLabelText("Adaptações"), "Tempo extra");
    await userEvent.click(screen.getByRole("button", { name: "Enviar para revisão" }));
    await waitFor(() => expect(enviar).toHaveBeenCalledWith("v1", "e2"));
    expect(salvar).toHaveBeenCalledWith("v1", expect.objectContaining({ adaptacoes: "Tempo extra", vigenciaInicio: "2026-02-01" }), "e1");
  });

  it("Salvar_duas_vezes_usa_a_versao_devolvida_pelo_put", async () => {
    salvar.mockResolvedValueOnce("e2").mockResolvedValueOnce("e3");
    montar(<ConteudoPeiForm versao={versao} />, "ProfissionalNap");
    await userEvent.click(screen.getByRole("button", { name: "Salvar rascunho" }));
    await screen.findByText("Rascunho salvo.");
    await userEvent.click(screen.getByRole("button", { name: "Salvar rascunho" }));
    await waitFor(() => expect(salvar).toHaveBeenLastCalledWith("v1", expect.anything(), "e2"));
    expect(obter).not.toHaveBeenCalled();
  });

  it("Conflito_409_oferece_recarregar_valores_atuais", async () => {
    salvar.mockRejectedValue({ status: 409, titulo: "Registro alterado por outra pessoa.", errosCampo: {} });
    const aoConflito = vi.fn();
    montar(<ConteudoPeiForm versao={versao} aoConflito={aoConflito} />, "ProfissionalNap");
    await userEvent.click(screen.getByRole("button", { name: "Salvar rascunho" }));
    await userEvent.click(await screen.findByRole("button", { name: "Recarregar valores atuais" }));
    expect(aoConflito).toHaveBeenCalledTimes(1);
    expect(screen.getByText(/substitui o texto não salvo/)).toBeInTheDocument();
  });

  it("Spinner_fica_no_botao_pressionado_ao_enviar", async () => {
    salvar.mockReturnValue(new Promise(() => {}));
    montar(<ConteudoPeiForm versao={versao} />, "ProfissionalNap");
    await userEvent.type(screen.getByLabelText("Adaptações"), "Tempo extra");
    await userEvent.click(screen.getByRole("button", { name: "Enviar para revisão" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Enviar para revisão" })).toHaveAttribute("aria-busy", "true"));
    expect(screen.getByRole("button", { name: "Salvar rascunho" })).not.toHaveAttribute("aria-busy");
    expect(screen.getByRole("button", { name: "Salvar rascunho" })).toBeDisabled();
  });

  it("Equipe_nap_edita_demanda_interna_vazia", () => {
    montar(<ConteudoPeiForm versao={versao} />, "ProfissionalNap");
    expect(screen.getByLabelText("Demanda interna")).toBeInTheDocument();
  });
});
