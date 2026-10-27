// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type InternalAxiosRequestConfig } from "axios";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { definirToken } from "../../lib/api";
import html from "../../../index.html?raw";
import { AssinarPeiPage } from "./AssinarPeiPage";
import { apiAssinatura } from "./services/assinaturaService";

type Resposta = { status: number; data?: unknown };
const adapterOriginal = apiAssinatura.defaults.adapter;
let pedidos: InternalAxiosRequestConfig[] = [];

function responder(fn: (url: string, corpo: Record<string, unknown>) => Resposta) {
  pedidos = [];
  apiAssinatura.defaults.adapter = async (config) => {
    pedidos.push(config);
    const r = fn(config.url ?? "", JSON.parse(String(config.data ?? "{}")));
    const resposta = { status: r.status, statusText: "", data: r.data, headers: {}, config };
    if (r.status >= 400) throw new AxiosError("falhou", String(r.status), config, null, resposta);
    return resposta;
  };
}

const versao = {
  aluno: "Maria Souza",
  numero: 2,
  conteudo: {
    demandaInterna: null,
    necessidadesEducacionais: "Leitura assistida.",
    adaptacoes: null,
    orientacoesProfessores: "Tempo extra nas provas.",
    recursosApoio: null,
    cuidadosPedagogicos: null,
    vigenciaInicio: "2026-02-01",
    vigenciaFim: "2026-07-31",
  },
  hashConteudo: "ab12",
  expiraEm: "2026-10-05T12:00:00Z",
  avisoAssinatura: "Assinatura demonstrativa — não certificada (pendência institucional)",
};

const invalido = { status: 404, data: { title: "Link inválido ou expirado." } };

function montar() {
  render(
    <MemoryRouter initialEntries={["/assinar/tok-secreto"]}>
      <Routes>
        <Route path="/assinar/:token" element={<AssinarPeiPage />} />
      </Routes>
    </MemoryRouter>,
  );
  return userEvent.setup();
}

async function abrirComCodigo(u: ReturnType<typeof userEvent.setup>, codigo = "123456") {
  await u.type(screen.getByLabelText("Digite o código de 6 dígitos"), codigo);
  await u.click(screen.getByRole("button", { name: "Continuar" }));
}

beforeEach(() => definirToken("token-da-equipe"));
afterEach(() => {
  apiAssinatura.defaults.adapter = adapterOriginal;
  definirToken(null);
});

describe("AssinarPeiPage", () => {
  it("Codigo_errado_mostra_tentativas_restantes", async () => {
    responder(() => ({ status: 400, data: { title: "Requisição inválida", errors: { codigo: ["Código incorreto. Restam 4 tentativas."] } } }));
    const u = montar();
    const campo = screen.getByLabelText("Digite o código de 6 dígitos");
    expect(campo).toHaveAttribute("inputmode", "numeric");
    expect(campo).toHaveAttribute("autocomplete", "one-time-code");
    await abrirComCodigo(u);
    expect(await screen.findByText("Código incorreto. Restam 4 tentativas.")).toBeInTheDocument();
    expect(campo).toHaveAttribute("aria-invalid", "true");
    // Token vai só no corpo; nada da sessão da equipe (Authorization, cookies).
    expect(pedidos[0]?.url).toBe("/assinar/abrir");
    expect(JSON.parse(String(pedidos[0]?.data))).toEqual({ token: "tok-secreto", codigo: "123456" });
    expect(pedidos[0]?.headers.get("Authorization")).toBeUndefined();
    expect(pedidos[0]?.withCredentials).toBeFalsy();
    expect(screen.queryByText(/tok-secreto/)).not.toBeInTheDocument();
  });

  it("Link_invalido_mostra_mensagem_generica", async () => {
    responder(() => invalido);
    const u = montar();
    await abrirComCodigo(u);
    expect(await screen.findByRole("heading", { name: "Link inválido ou expirado" })).toBeInTheDocument();
    expect(screen.getByText("Peça um novo link ao NAP.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Digite o código de 6 dígitos")).not.toBeInTheDocument();
  });

  it("Assinar_exige_nome_e_aceite", async () => {
    responder(() => ({ status: 200, data: versao }));
    const u = montar();
    await abrirComCodigo(u);
    expect(await screen.findByRole("heading", { name: "Necessidades educacionais" })).toBeInTheDocument();
    expect(screen.getByText("Tempo extra nas provas.")).toBeInTheDocument();
    expect(screen.getByText(versao.avisoAssinatura)).toBeInTheDocument();

    await u.click(screen.getByRole("button", { name: "Assinar" }));
    expect(screen.getByLabelText(/^Nome completo/)).toHaveAccessibleDescription("Informe seu nome completo.");
    expect(screen.getByLabelText("Li e concordo com este Plano Educacional Individualizado")).toHaveAccessibleDescription(
      "Marque a caixa para concordar com o PEI.",
    );
    expect(pedidos).toHaveLength(1);
  });

  it("Sucesso_mostra_confirmacao_sem_dados_da_ficha", async () => {
    responder((url) => (url === "/assinar/abrir" ? { status: 200, data: versao } : { status: 204 }));
    const u = montar();
    await abrirComCodigo(u);
    await u.type(await screen.findByLabelText(/^Nome completo/), "Maria Souza");
    await u.click(screen.getByLabelText("Li e concordo com este Plano Educacional Individualizado"));
    await u.click(screen.getByRole("button", { name: "Assinar" }));

    expect(await screen.findByRole("heading", { name: "Assinatura registrada" })).toHaveFocus();
    expect(JSON.parse(String(pedidos[1]?.data))).toEqual({ token: "tok-secreto", codigo: "123456", nomeCompleto: "Maria Souza", aceite: true });
    expect(pedidos[1]?.headers.get("Authorization")).toBeUndefined();
    expect(screen.queryByText(/Maria Souza/)).not.toBeInTheDocument();
    expect(screen.queryByText("Tempo extra nas provas.")).not.toBeInTheDocument();
  });

  it("Codigo_colado_com_separadores_vira_seis_digitos", async () => {
    responder(() => invalido);
    const u = montar();
    const campo = screen.getByLabelText("Digite o código de 6 dígitos");
    await u.click(campo);
    await u.paste("123 456");
    expect(campo).toHaveValue("123456");
    await u.clear(campo);
    await u.paste("123-4567");
    expect(campo).toHaveValue("123456");
  });

  it("Erro_de_servidor_mostra_alerta_geral", async () => {
    responder(() => ({ status: 503 }));
    const u = montar();
    await abrirComCodigo(u);
    expect(await screen.findByRole("alert")).toHaveTextContent("Servidor indisponível");
    expect(screen.getByLabelText("Digite o código de 6 dígitos")).not.toHaveAttribute("aria-invalid");
  });

  it("Index_html_pede_no_referrer_antes_de_qualquer_recurso", () => {
    // O token está na URL: favicon, scripts e logo não podem levar Referer /assinar/<token>.
    const meta = html.indexOf('<meta name="referrer" content="no-referrer" />');
    expect(meta).toBeGreaterThan(-1);
    expect(meta).toBeLessThan(html.indexOf("<link"));
    expect(meta).toBeLessThan(html.indexOf("<script"));
  });
});
