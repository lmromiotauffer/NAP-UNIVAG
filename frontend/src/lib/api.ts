import axios, { isAxiosError, type InternalAxiosRequestConfig } from "axios";
import type { ConflitoAgenda, Sessao } from "./types";

// Sempre a mesma origem: o cookie de refresh (SameSite=Strict, Path=/api/auth) não funcionaria com a API em outro site.
export const api = axios.create({
  baseURL: "/api",
  withCredentials: true,
  timeout: 30_000,
});

// O token de acesso vive só em memória: nunca em localStorage/sessionStorage.
let token: string | null = null;
// Dono do token: um refresh que traga outro usuário (cookie trocado em outra aba) não pode reenviar pedidos do anterior.
let usuarioDoToken: string | null = null;
// Geração da sessão: muda a cada troca explícita de identidade (login, logout, expiração).
// Um refresh iniciado numa geração anterior é obsoleto e tem o resultado descartado.
let geracao = 0;
/** Troca explícita de identidade. O refresh usa a atribuição interna e não muda a geração. */
export const definirToken = (t: string | null, usuarioId: string | null = null): void => {
  token = t;
  usuarioDoToken = usuarioId;
  geracao++;
};

let ouvintesExpiracao: Array<() => void> = [];
export function aoSessaoExpirada(cb: () => void): () => void {
  ouvintesExpiracao.push(cb);
  return () => {
    ouvintesExpiracao = ouvintesExpiracao.filter((o) => o !== cb);
  };
}

let ouvintesRenovacao: Array<(s: Sessao) => void> = [];
/** Avisa toda renovação bem-sucedida: o cookie é compartilhado entre abas e pode trazer outro usuário. */
export function aoSessaoRenovada(cb: (s: Sessao) => void): () => void {
  ouvintesRenovacao.push(cb);
  return () => {
    ouvintesRenovacao = ouvintesRenovacao.filter((o) => o !== cb);
  };
}

let renovacaoEmVoo: Promise<Sessao | null> | null = null;
let geracaoEmVoo = -1;

/**
 * Uma única promessa compartilhada enquanto o refresh está em voo. Resolve `null` só quando a API recusa o cookie (4xx);
 * falha de rede ou do servidor rejeita, porque a sessão pode continuar válida.
 */
export function renovarSessao(): Promise<Sessao | null> {
  // Refresh em voo de uma geração anterior não serve: inicia outro.
  if (renovacaoEmVoo && geracaoEmVoo === geracao) return renovacaoEmVoo;
  const inicio = geracao;
  geracaoEmVoo = inicio;
  const promessa: Promise<Sessao | null> = api
    // Sem timeout: a API gira o token mesmo se o cliente desistir; abortar perderia o cookie novo e o próximo refresh
    // (com o antigo, fora da tolerância de 5 s) seria tratado como reuso, revogando a sessão.
    .post<Sessao>("/auth/refresh", undefined, { headers: { "X-Requested-With": "nap-spa" }, timeout: 0 })
    .then((r) => {
      // Login ou logout durante o refresh: descarta (sem token, sem aviso), senão reautentica ou troca o usuário.
      if (geracao !== inicio) return null;
      token = r.data.accessToken;
      usuarioDoToken = r.data.usuario.id;
      for (const ouvinte of [...ouvintesRenovacao]) ouvinte(r.data);
      return r.data;
    })
    .catch((e: unknown) => {
      if (isAxiosError(e) && e.response && e.response.status < 500) return null;
      throw e;
    })
    .finally(() => {
      if (renovacaoEmVoo === promessa) renovacaoEmVoo = null;
    });
  renovacaoEmVoo = promessa;
  return promessa;
}

type ConfigRenovavel = InternalAxiosRequestConfig & { _renovada?: boolean; _geracao?: number; _usuario?: string | null };

api.interceptors.request.use((config: ConfigRenovavel) => {
  if (token) config.headers.set("Authorization", `Bearer ${token}`);
  config._geracao = geracao;
  config._usuario = usuarioDoToken;
  return config;
});

api.interceptors.response.use(undefined, async (erro: unknown) => {
  if (!isAxiosError(erro) || erro.response?.status !== 401 || !erro.config) throw erro;
  const config: ConfigRenovavel = erro.config;
  // Só login, refresh e logout ficam fora: /auth/senha e /auth/eu são autenticados e renovam como o resto.
  if (/^\/auth\/(login|refresh|logout)$/.test(config.url ?? "") || config._renovada) throw erro;

  const geracaoDoPedido = config._geracao ?? geracao;
  const renovacao = renovarSessao();
  let sessao: Sessao | null;
  try {
    sessao = await renovacao;
  } catch (falha) {
    // Rede ou servidor fora: a sessão segue (sem derrubar o usuário). O chamador recebe a falha do refresh (status 0 ou 5xx),
    // que vira "servidor indisponível" e uma retentativa automática, em vez de "Sessão expirada" com a sessão válida.
    throw falha;
  }
  if (!sessao) {
    // Identidade trocada enquanto esperava (login/logout): não derruba a sessão nova.
    if (geracao !== geracaoDoPedido) throw erro;
    // definirToken muda a geração: os demais 401 que esperavam este refresh saem na checagem acima, então o aviso sai uma vez só.
    definirToken(null);
    for (const ouvinte of [...ouvintesExpiracao]) ouvinte();
    throw erro;
  }
  // Enviado como um usuário, renovado como outro: reenviar agiria em nome de quem não pediu.
  if (config._usuario && sessao.usuario.id !== config._usuario) throw erro;
  config._renovada = true;
  config.headers.set("Authorization", `Bearer ${sessao.accessToken}`);
  return api(config);
});

/** Concorrência otimista: a API exige a versão lida no cabeçalho If-Match das edições. */
export const comVersao = (versao: string) => ({ headers: { "If-Match": `"${versao}"` } });

/** Criações: a mesma chave reenviada não duplica o registro (a API devolve o mesmo id). */
export const comChave = (chave: string) => ({ headers: { "Idempotency-Key": chave } });

export type ErroApi = {
  status: number;
  titulo: string;
  detalhe?: string;
  errosCampo: Record<string, string[]>;
  conflitos?: ConflitoAgenda[];
  traceId?: string;
};

const TITULOS: Record<number, string> = {
  401: "Sessão expirada",
  403: "Acesso negado",
  404: "Não encontrado",
  409: "Conflito",
  500: "Erro interno do servidor",
  502: "Servidor indisponível",
  503: "Servidor indisponível",
  504: "O servidor demorou demais para responder",
};

function ehErroApi(e: unknown): e is ErroApi {
  return typeof e === "object" && e !== null && "errosCampo" in e && "titulo" in e && "status" in e;
}

export function normalizarErro(e: unknown): ErroApi {
  if (ehErroApi(e)) return e;
  if (!isAxiosError(e)) {
    return { status: 0, titulo: e instanceof Error ? e.message : "Erro inesperado", errosCampo: {} };
  }
  if (!e.response) {
    const timeout = e.code === "ECONNABORTED" || e.code === "ETIMEDOUT";
    return {
      status: 0,
      titulo: timeout ? "O servidor demorou demais para responder" : "Não foi possível conectar ao servidor",
      errosCampo: {},
    };
  }
  const { status, data } = e.response;
  const p = (typeof data === "object" && data !== null ? data : {}) as {
    title?: string;
    detail?: string;
    traceId?: string;
    errors?: Record<string, string[]>;
    conflitos?: ConflitoAgenda[];
  };
  return {
    status,
    titulo: p.title ?? TITULOS[status] ?? "Erro inesperado",
    ...(p.detail ? { detalhe: p.detail } : {}),
    errosCampo: p.errors ?? {},
    ...(Array.isArray(p.conflitos) ? { conflitos: p.conflitos } : {}),
    ...(p.traceId ? { traceId: p.traceId } : {}),
  };
}

export function mensagemErro(e: unknown): string {
  const erro = normalizarErro(e);
  return erro.detalhe ?? erro.titulo;
}

/** Mapeia erros da API: campos conhecidos vão para `setError`; 409, 422 e o resto vão para o Alert, mantendo o formulário. */
export function tratarErro<T extends string>(e: unknown, campos: readonly T[], marcar: (campo: T, mensagem: string) => void, mapa: Record<string, T> = {}): string | null {
  const erro = normalizarErro(e);
  let algumCampo = false;
  const semCampo: string[] = [];
  for (const [campoApi, mensagens] of Object.entries(erro.errosCampo)) {
    const mensagem = mensagens[0] ?? erro.titulo;
    const campo = mapa[campoApi] ?? (campoApi as T);
    if (campos.includes(campo)) {
      marcar(campo, mensagem);
      algumCampo = true;
    } else semCampo.push(mensagem);
  }
  if (semCampo.length > 0) return semCampo.join(" ");
  return algumCampo ? null : mensagemErro(e);
}
