import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { aoSessaoExpirada, aoSessaoRenovada, definirToken, renovarSessao } from "../lib/api";
import type { UsuarioSessao } from "../lib/types";
import { authService } from "./authService";
import { SessaoContexto, type ContextoSessao, type EstadoSessao } from "./useSessao";

/**
 * Sem dependência do roteador: ao ficar anônimo, a RotaProtegida redireciona para /entrar?voltar=.
 * Entrar, sair, sessão expirada e troca de usuário limpam o cache de consultas.
 */
export function SessaoProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [estado, setEstado] = useState<EstadoSessao>("carregando");
  // Espelho síncrono do estado: o resultado do boot não pode sobrescrever um login já concluído.
  const estadoAtual = useRef<EstadoSessao>("carregando");
  const [usuario, setUsuario] = useState<UsuarioSessao | null>(null);
  const [saiuPorVontade, setSaiuPorVontade] = useState(false);
  const ultimoUsuarioId = useRef<string | null>(null);
  const iniciou = useRef(false);

  const autenticar = useCallback(
    (u: UsuarioSessao) => {
      if (ultimoUsuarioId.current !== null && ultimoUsuarioId.current !== u.id) queryClient.clear();
      ultimoUsuarioId.current = u.id;
      setUsuario(u);
      setSaiuPorVontade(false);
      estadoAtual.current = "autenticado";
      setEstado("autenticado");
    },
    [queryClient],
  );

  const encerrar = useCallback(() => {
    definirToken(null);
    queryClient.clear();
    setUsuario(null);
    estadoAtual.current = "anonimo";
    setEstado("anonimo");
  }, [queryClient]);

  useEffect(() => {
    // StrictMode monta duas vezes: o ref garante um único refresh (e sem cancelamento, o resultado não se perde).
    if (iniciou.current) return;
    iniciou.current = true;
    // Assinatura do aluno é anônima e isolada: não toca no cookie da equipe que use o mesmo navegador.
    if (window.location.pathname.startsWith("/assinar/")) return;
    // Sessão válida já chega por aoSessaoRenovada; aqui só resolve o caso sem cookie. Sem resposta (rede ou servidor
    // fora) também fica anônimo: no boot não há sessão a preservar, e a tela não pode ficar carregando para sempre.
    void renovarSessao().catch(() => null).then((sessao) => {
      if (!sessao && estadoAtual.current === "carregando") {
        estadoAtual.current = "anonimo";
        setEstado("anonimo");
      }
    });
  }, []);

  useEffect(() => aoSessaoExpirada(encerrar), [encerrar]);
  // Toda renovação (boot ou interceptor) reflete o usuário do cookie; id diferente limpa o cache em autenticar.
  useEffect(
    () =>
      aoSessaoRenovada((s) => {
        // Defesa em profundidade: depois de sair/expirar, só login ou boot autenticam de novo.
        if (estadoAtual.current === "anonimo") return;
        autenticar(s.usuario);
      }),
    [autenticar],
  );

  const entrar = useCallback(
    async (email: string, senha: string) => {
      const sessao = await authService.entrar(email, senha);
      queryClient.clear();
      autenticar(sessao.usuario);
    },
    [autenticar, queryClient],
  );

  const trocarSenha = useCallback(
    async (senhaAtual: string, novaSenha: string) => {
      const sessao = await authService.trocarSenha(senhaAtual, novaSenha);
      autenticar(sessao.usuario);
    },
    [autenticar],
  );

  const sair = useCallback(async () => {
    await authService.sair();
    setSaiuPorVontade(true);
    encerrar();
  }, [encerrar]);

  const valor = useMemo<ContextoSessao>(() => ({ estado, usuario, saiuPorVontade, entrar, trocarSenha, sair }), [estado, usuario, saiuPorVontade, entrar, trocarSenha, sair]);
  return <SessaoContexto value={valor}>{children}</SessaoContexto>;
}
