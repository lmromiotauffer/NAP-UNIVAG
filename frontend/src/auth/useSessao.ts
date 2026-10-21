import { createContext, useContext } from "react";
import type { UsuarioSessao } from "../lib/types";

export type EstadoSessao = "carregando" | "anonimo" | "autenticado";

export type ContextoSessao = {
  estado: EstadoSessao;
  usuario: UsuarioSessao | null;
  /** true após "Sair": o login abre sem `voltar` (não expõe a rota do usuário anterior). */
  saiuPorVontade: boolean;
  entrar(email: string, senha: string): Promise<void>;
  /** Atualiza o token e o usuário da sessão (zera `deveTrocarSenha`). */
  trocarSenha(senhaAtual: string, novaSenha: string): Promise<void>;
  sair(): Promise<void>;
};

export const SessaoContexto = createContext<ContextoSessao | null>(null);

export function useSessao(): ContextoSessao {
  const contexto = useContext(SessaoContexto);
  if (!contexto) throw new Error("useSessao precisa estar dentro de <SessaoProvider>.");
  return contexto;
}
