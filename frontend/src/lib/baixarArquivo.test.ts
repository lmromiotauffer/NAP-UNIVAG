// @vitest-environment jsdom
import { AxiosError, type AxiosAdapter } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { api, type ErroApi } from "./api";
import { baixarArquivo } from "./baixarArquivo";

const adapterOriginal = api.defaults.adapter;
let nomesBaixados: string[];

function responder(status: number, data: Blob, headers: Record<string, string> = {}) {
  const adapter: AxiosAdapter = async (config) => {
    const resposta = { status, statusText: "", data, headers, config };
    if (status >= 400) throw new AxiosError("falhou", String(status), config, null, resposta);
    return resposta;
  };
  api.defaults.adapter = adapter;
}

beforeEach(() => {
  nomesBaixados = [];
  URL.createObjectURL = vi.fn(() => "blob:teste");
  URL.revokeObjectURL = vi.fn();
  vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
    nomesBaixados.push(this.download);
  });
});
afterEach(() => {
  api.defaults.adapter = adapterOriginal;
  vi.restoreAllMocks();
});

describe("baixarArquivo", () => {
  it("Extrai_nome_utf8_do_content_disposition", async () => {
    responder(200, new Blob(["x"]), { "content-disposition": "attachment; filename=\"laudo.pdf\"; filename*=UTF-8''Relat%C3%B3rio%20final.pdf" });
    await baixarArquivo("/documentos/d1/arquivo");
    expect(nomesBaixados).toEqual(["Relatório final.pdf"]);
  });

  it("Usa_filename_simples_e_depois_o_padrao", async () => {
    responder(200, new Blob(["x"]), { "content-disposition": 'attachment; filename="laudo.pdf"' });
    await baixarArquivo("/a");
    responder(200, new Blob(["x"]));
    await baixarArquivo("/a");
    expect(nomesBaixados).toEqual(["laudo.pdf", "arquivo"]);
  });

  it("Erro_em_blob_vira_problem_details_e_nao_baixa", async () => {
    responder(400, new Blob([JSON.stringify({ title: "Erro", errors: { arquivo: ["Formato inválido."] } })], { type: "application/json" }));
    const erro = (await baixarArquivo("/a").catch((e: unknown) => e)) as ErroApi;
    expect(erro.status).toBe(400);
    expect(erro.errosCampo["arquivo"]).toEqual(["Formato inválido."]);
    expect(URL.createObjectURL).not.toHaveBeenCalled();
    expect(nomesBaixados).toEqual([]);
  });

  it("Revoga_a_url_temporaria_depois_do_clique", async () => {
    vi.useFakeTimers({ toFake: ["setTimeout"] });
    try {
      responder(200, new Blob(["x"]));
      await baixarArquivo("/a");
      expect(URL.revokeObjectURL).not.toHaveBeenCalled();
      vi.runAllTimers();
      expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:teste");
    } finally {
      vi.useRealTimers();
    }
  });
});
