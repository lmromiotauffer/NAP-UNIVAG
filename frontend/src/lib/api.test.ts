import { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { api, aoSessaoExpirada, definirToken, mensagemErro, normalizarErro, tratarErro, type ErroApi } from "./api";

type Resposta = { status: number; data?: unknown };
type Chamada = { url: string; auth: string | undefined };

const sessao = { accessToken: "novo", expiraEm: "2030-01-01T00:00:00Z", usuario: {} };
const adapterOriginal = api.defaults.adapter;

function instalar(responder: (c: InternalAxiosRequestConfig, n: number) => Resposta | Promise<Resposta>) {
  const chamadas: Chamada[] = [];
  const adapter: AxiosAdapter = async (config) => {
    chamadas.push({ url: config.url ?? "", auth: config.headers.get("Authorization") as string | undefined });
    const r = await responder(config, chamadas.length);
    const resposta = { status: r.status, statusText: "", data: r.data, headers: {}, config };
    if (r.status >= 400) throw new AxiosError("falhou", String(r.status), config, null, resposta);
    return resposta;
  };
  api.defaults.adapter = adapter;
  return chamadas;
}

beforeEach(() => definirToken("velho"));
afterEach(() => {
  api.defaults.adapter = adapterOriginal;
  definirToken(null);
  vi.restoreAllMocks();
});

describe("renovação de sessão", () => {
  it("dois_401_simultaneos_disparam_um_unico_refresh", async () => {
    const chamadas = instalar(async (c) => {
      if (c.url === "/auth/refresh") {
        await new Promise((r) => setTimeout(r, 10));
        return { status: 200, data: sessao };
      }
      return c.headers.get("Authorization") === "Bearer novo" ? { status: 200, data: "ok" } : { status: 401 };
    });
    await Promise.all([api.get("/a"), api.get("/b")]);
    expect(chamadas.filter((c) => c.url === "/auth/refresh")).toHaveLength(1);
  });

  it("refresh_falho_encerra_sessao_sem_loop", async () => {
    const chamadas = instalar((c) => (c.url === "/auth/refresh" ? { status: 401 } : { status: 401 }));
    const cb = vi.fn();
    const cancelar = aoSessaoExpirada(cb);
    await expect(api.get("/a")).rejects.toBeTruthy();
    cancelar();
    expect(cb).toHaveBeenCalledTimes(1);
    expect(chamadas.filter((c) => c.url === "/auth/refresh")).toHaveLength(1);
    const seguintes = instalar(() => ({ status: 200 }));
    await api.get("/b");
    expect(seguintes[0]?.auth).toBeUndefined();
  });

  it("dois_401_com_refresh_falho_notificam_uma_vez", async () => {
    instalar(async (c) => {
      if (c.url === "/auth/refresh") {
        await new Promise((r) => setTimeout(r, 10));
        return { status: 401 };
      }
      return { status: 401 };
    });
    const cb = vi.fn();
    const cancelar = aoSessaoExpirada(cb);
    await Promise.allSettled([api.get("/a"), api.get("/b")]);
    cancelar();
    expect(cb).toHaveBeenCalledTimes(1);
  });

  it("refresh_com_falha_do_servidor_nao_encerra_a_sessao", async () => {
    const chamadas = instalar((c) => (c.url === "/auth/refresh" ? { status: 503 } : { status: 401 }));
    const cb = vi.fn();
    const cancelar = aoSessaoExpirada(cb);
    // O chamador recebe a falha do refresh (503), não o 401: a tela diz "servidor indisponível", não "sessão expirada".
    await expect(api.get("/a")).rejects.toMatchObject({ response: { status: 503 } });
    cancelar();
    expect(cb).not.toHaveBeenCalled();
    expect(chamadas.filter((c) => c.url === "/auth/refresh")).toHaveLength(1);
    // O cookie continua válido: a próxima requisição ainda leva o token em memória.
    const seguintes = instalar(() => ({ status: 200 }));
    await api.get("/b");
    expect(seguintes[0]?.auth).toBe("Bearer velho");
  });

  it("requisicao_repetida_leva_novo_token", async () => {
    const chamadas = instalar((c) => {
      if (c.url === "/auth/refresh") return { status: 200, data: sessao };
      return c.headers.get("Authorization") === "Bearer novo" ? { status: 200, data: "ok" } : { status: 401 };
    });
    await api.get("/a");
    expect(chamadas.filter((c) => c.url === "/a").map((c) => c.auth)).toEqual(["Bearer velho", "Bearer novo"]);
    await api.get("/b");
    expect(chamadas.find((c) => c.url === "/b")?.auth).toBe("Bearer novo");
  });

  it("refresh_que_traz_outro_usuario_nao_repete_o_pedido", async () => {
    let dono = "u2";
    const chamadas = instalar((c) => {
      if (c.url === "/auth/refresh") return { status: 200, data: { ...sessao, usuario: { id: dono } } };
      return c.headers.get("Authorization") === "Bearer novo" ? { status: 200, data: "ok" } : { status: 401 };
    });
    definirToken("velho", "u1");
    await expect(api.post("/a", {})).rejects.toMatchObject({ response: { status: 401 } });
    expect(chamadas.filter((c) => c.url === "/a")).toHaveLength(1);

    dono = "u1";
    definirToken("velho", "u1");
    await api.post("/b", {});
    expect(chamadas.filter((c) => c.url === "/b").map((c) => c.auth)).toEqual(["Bearer velho", "Bearer novo"]);
  });

  it("auth_senha_vencida_renova_e_repete_mas_login_nao_renova", async () => {
    const chamadas = instalar((c) => {
      if (c.url === "/auth/refresh") return { status: 200, data: sessao };
      if (c.url === "/auth/login") return { status: 401 };
      return c.headers.get("Authorization") === "Bearer novo" ? { status: 200, data: "ok" } : { status: 401 };
    });
    await api.post("/auth/senha", {});
    expect(chamadas.filter((c) => c.url === "/auth/senha").map((c) => c.auth)).toEqual(["Bearer velho", "Bearer novo"]);
    expect(chamadas.filter((c) => c.url === "/auth/refresh")).toHaveLength(1);
    await expect(api.post("/auth/login", {})).rejects.toBeTruthy();
    expect(chamadas.filter((c) => c.url === "/auth/refresh")).toHaveLength(1);
  });
});

describe("normalização de erros", () => {
  it("normaliza_problem_details_com_errors", async () => {
    instalar(() => ({
      status: 422,
      data: { title: "Dados inválidos", detail: "Corrija", traceId: "t1", errors: { nome: ["Obrigatório"] } },
    }));
    const e = await api.get("/a").catch((x: unknown) => normalizarErro(x));
    expect(e).toEqual({
      status: 422,
      titulo: "Dados inválidos",
      detalhe: "Corrija",
      errosCampo: { nome: ["Obrigatório"] },
      traceId: "t1",
    });
    expect(mensagemErro(e)).toBe("Corrija");
  });

  it("erro_de_rede_vira_status_0", () => {
    const e = normalizarErro(new AxiosError("Network Error", "ERR_NETWORK"));
    expect(e.status).toBe(0);
    expect(e.titulo).toBe("Não foi possível conectar ao servidor");
  });
});

describe("tratarErro", () => {
  const erro = (errosCampo: Record<string, string[]>): ErroApi => ({ status: 400, titulo: "Dados inválidos", errosCampo });

  it("campo_exibido_vai_para_o_campo_e_o_nao_exibido_vai_para_o_alert", () => {
    const marcados: [string, string][] = [];
    const geral = tratarErro(erro({ motivo: ["Motivo curto."], cursoId: ["Curso inativo."] }), ["motivo"] as const, (c, m) => marcados.push([c, m]));
    expect(marcados).toEqual([["motivo", "Motivo curto."]]);
    expect(geral).toBe("Curso inativo.");
  });

  it("so_campos_exibidos_nao_gera_alert_e_sem_campos_usa_a_mensagem_geral", () => {
    expect(tratarErro(erro({ inicio: ["Fim de semana."] }), ["data"] as const, () => {}, { inicio: "data" })).toBeNull();
    expect(tratarErro({ status: 409, titulo: "Conflito", detalhe: "Matrícula já cadastrada.", errosCampo: {} }, ["motivo"] as const, () => {})).toBe("Matrícula já cadastrada.");
  });
});
