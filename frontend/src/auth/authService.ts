import { api, definirToken } from "../lib/api";
import type { Sessao } from "../lib/types";

const CABECALHO_SPA = { "X-Requested-With": "nap-spa" };

export const authService = {
  async entrar(email: string, senha: string): Promise<Sessao> {
    const { data } = await api.post<Sessao>("/auth/login", { email, senha });
    definirToken(data.accessToken, data.usuario.id);
    return data;
  },
  /** Devolve a sessão nova (mesmo contrato do login): o token anterior deixa de valer no servidor. */
  async trocarSenha(senhaAtual: string, novaSenha: string): Promise<Sessao> {
    const { data } = await api.post<Sessao>("/auth/senha", { senhaAtual, novaSenha });
    definirToken(data.accessToken, data.usuario.id);
    return data;
  },
  /** Anônimo no backend (cookie + cabeçalho). O token sai antes da chamada: invalida refresh em voo. */
  async sair(): Promise<void> {
    definirToken(null);
    try {
      await api.post("/auth/logout", undefined, { headers: CABECALHO_SPA });
    } catch {
      // Sessão local é encerrada de qualquer forma.
    }
  },
};
