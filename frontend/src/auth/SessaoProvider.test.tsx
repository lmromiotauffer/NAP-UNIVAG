// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from "axios";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { api, definirToken } from "../lib/api";
import { SessaoProvider } from "./SessaoProvider";
import { useSessao } from "./useSessao";

const adapterOriginal = api.defaults.adapter;

const usuarioDe = (id: string) => ({ id, nome: `Usuário ${id}`, email: `${id}@nap.demo`, perfil: "CoordenadorNap", cursoId: null, curso: null });
const sessaoDe = (id: string) => ({ accessToken: `token-${id}`, expiraEm: "2030-01-01T00:00:00Z", usuario: usuarioDe(id) });

type Resposta = { status: number; data?: unknown };

function instalar(responder: (c: InternalAxiosRequestConfig) => Resposta | Promise<Resposta>) {
  const adapter: AxiosAdapter = async (config) => {
    const r = await responder(config);
    const resposta = { status: r.status, statusText: "", data: r.data, headers: {}, config };
    if (r.status >= 400) throw new AxiosError("falhou", String(r.status), config, null, resposta);
    return resposta;
  };
  api.defaults.adapter = adapter;
}

/** Authorization que a próxima requisição levaria (o token vive só em memória no módulo). */
async function tokenAtual(): Promise<unknown> {
  let auth: unknown;
  api.defaults.adapter = async (config) => {
    auth = config.headers.get("Authorization");
    return { status: 200, statusText: "", data: null, headers: {}, config };
  };
  await api.get("/sonda");
  return auth;
}

function montar() {
  const queryClient = new QueryClient();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <SessaoProvider>{children}</SessaoProvider>
    </QueryClientProvider>
  );
  return { queryClient, ...renderHook(() => useSessao(), { wrapper }) };
}

afterEach(() => {
  api.defaults.adapter = adapterOriginal;
  definirToken(null);
});

describe("SessaoProvider", () => {
  it("boot_sem_cookie_fica_anonimo", async () => {
    instalar(() => ({ status: 401 }));
    const { result } = montar();
    expect(result.current.estado).toBe("carregando");
    await waitFor(() => expect(result.current.estado).toBe("anonimo"));
    expect(result.current.usuario).toBeNull();
  });

  it("boot_com_servidor_fora_fica_anonimo_em_vez_de_carregando", async () => {
    instalar(() => ({ status: 503 }));
    const { result } = montar();
    await waitFor(() => expect(result.current.estado).toBe("anonimo"));
  });

  it("Rota_de_assinatura_nao_chama_refresh_de_sessao", async () => {
    const urls: Array<string | undefined> = [];
    instalar((c) => {
      urls.push(c.url);
      return { status: 401 };
    });
    window.history.pushState({}, "", "/assinar/tok");
    try {
      montar();
      await new Promise((r) => setTimeout(r, 0));
      expect(urls).toEqual([]);
    } finally {
      window.history.pushState({}, "", "/");
    }
  });

  it("logout_limpa_cache_de_consultas", async () => {
    instalar((c) => (c.url === "/auth/refresh" ? { status: 200, data: sessaoDe("a") } : { status: 204 }));
    const { result, queryClient } = montar();
    await waitFor(() => expect(result.current.estado).toBe("autenticado"));
    queryClient.setQueryData(["alunos", "lista"], [1, 2, 3]);
    expect(queryClient.getQueryCache().getAll().length).toBe(1);
    await act(() => result.current.sair());
    expect(queryClient.getQueryCache().getAll().length).toBe(0);
    expect(result.current.estado).toBe("anonimo");
    expect(result.current.saiuPorVontade).toBe(true);
  });

  it("logout_limpa_sessao_mesmo_se_a_api_falhar", async () => {
    instalar((c) => (c.url === "/auth/refresh" ? { status: 200, data: sessaoDe("a") } : { status: 500 }));
    const { result, queryClient } = montar();
    await waitFor(() => expect(result.current.estado).toBe("autenticado"));
    queryClient.setQueryData(["painel"], {});
    await act(() => result.current.sair());
    expect(queryClient.getQueryCache().getAll().length).toBe(0);
    expect(result.current.usuario).toBeNull();
  });

  it("troca_de_usuario_limpa_cache", async () => {
    instalar((c) => {
      if (c.url === "/auth/refresh") return { status: 200, data: sessaoDe("a") };
      if (c.url === "/auth/login") return { status: 200, data: sessaoDe("b") };
      return { status: 204 };
    });
    const { result, queryClient } = montar();
    await waitFor(() => expect(result.current.usuario?.id).toBe("a"));
    queryClient.setQueryData(["alunos", "ficha", "x"], { nome: "dado do usuário a" });
    await act(() => result.current.entrar("b@nap.demo", "segredo"));
    expect(result.current.usuario?.id).toBe("b");
    expect(queryClient.getQueryCache().getAll().length).toBe(0);
  });

  it("refresh_do_interceptor_com_outro_usuario_atualiza_e_limpa_cache", async () => {
    let usuarioDoCookie = "a";
    instalar((c) => {
      if (c.url === "/auth/refresh") return { status: 200, data: sessaoDe(usuarioDoCookie) };
      return c.headers.get("Authorization") === "Bearer token-b" ? { status: 200, data: "ok" } : { status: 401 };
    });
    const { result, queryClient } = montar();
    await waitFor(() => expect(result.current.usuario?.id).toBe("a"));
    queryClient.setQueryData(["alunos", "ficha", "x"], { nome: "dado do usuário a" });
    usuarioDoCookie = "b"; // outra aba entrou como B e trocou o cookie
    await act(() => api.get("/alunos"));
    expect(result.current.usuario?.id).toBe("b");
    expect(queryClient.getQueryCache().getAll().length).toBe(0);
  });

  it("sessao_expirada_limpa_cache_e_fica_anonimo", async () => {
    let refreshes = 0;
    instalar((c) => {
      if (c.url === "/auth/refresh") return ++refreshes === 1 ? { status: 200, data: sessaoDe("a") } : { status: 401 };
      return { status: 401 };
    });
    const { result, queryClient } = montar();
    await waitFor(() => expect(result.current.estado).toBe("autenticado"));
    queryClient.setQueryData(["painel"], { total: 1 });
    await act(async () => {
      await expect(api.get("/painel")).rejects.toBeTruthy();
    });
    expect(queryClient.getQueryCache().getAll().length).toBe(0);
    expect(result.current.estado).toBe("anonimo");
    expect(result.current.saiuPorVontade).toBe(false);
  });

  it("refresh_lento_do_boot_nao_sobrescreve_login", async () => {
    let liberarRefresh: (r: Resposta) => void = () => {};
    instalar((c) => {
      if (c.url === "/auth/refresh") return new Promise<Resposta>((r) => (liberarRefresh = r));
      if (c.url === "/auth/login") return { status: 200, data: sessaoDe("b") };
      return { status: 204 };
    });
    const { result } = montar();
    await act(() => result.current.entrar("b@nap.demo", "segredo"));
    expect(result.current.usuario?.id).toBe("b");
    await act(async () => {
      liberarRefresh({ status: 200, data: sessaoDe("a") });
      await new Promise((r) => setTimeout(r, 0));
    });
    expect(await tokenAtual()).toBe("Bearer token-b");
    expect(result.current.usuario?.id).toBe("b");
    expect(result.current.estado).toBe("autenticado");
  });

  it("logout_com_refresh_em_voo_continua_anonimo", async () => {
    let refreshes = 0;
    let liberarRefresh: (r: Resposta) => void = () => {};
    instalar((c) => {
      if (c.url === "/auth/refresh") {
        if (++refreshes === 1) return { status: 200, data: sessaoDe("a") };
        return new Promise<Resposta>((r) => (liberarRefresh = r));
      }
      if (c.url === "/auth/logout") return { status: 204 };
      return { status: 401 };
    });
    const { result } = montar();
    await waitFor(() => expect(result.current.estado).toBe("autenticado"));
    const consulta = api.get("/painel").catch(() => "falhou");
    await waitFor(() => expect(refreshes).toBe(2));
    await act(() => result.current.sair());
    await act(async () => {
      liberarRefresh({ status: 200, data: sessaoDe("a") });
      expect(await consulta).toBe("falhou");
    });
    expect(result.current.estado).toBe("anonimo");
    expect(result.current.usuario).toBeNull();
    expect(await tokenAtual()).toBeUndefined();
  });
});
