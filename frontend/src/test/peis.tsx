import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { StrictMode, type ReactElement } from "react";
import { MemoryRouter } from "react-router";
import { vi } from "vitest";
import { SessaoContexto, type ContextoSessao } from "../auth/useSessao";
import { ToastProvider } from "../components/ui/Toast";
import type { VersaoPeiDetalhe } from "../lib/types";

export const acoesNenhuma = {
  podeEditar: false,
  podeEnviarRevisao: false,
  podeAprovar: false,
  podeDevolver: false,
  podeAssinarComoProfissional: false,
  podeGerarAcessoAluno: false,
  podePublicar: false,
  podeArquivar: false,
  podeRegistrarCiencia: false,
  podeCriarNovaVersao: false,
};

export const versaoBase: VersaoPeiDetalhe = {
  id: "v1",
  peiId: "p1",
  alunoId: "a1",
  aluno: "Maria Souza",
  matricula: "2024001",
  curso: "Direito",
  responsavel: "Ana Beatriz",
  numero: 1,
  estado: "Rascunho",
  conteudo: {
    demandaInterna: null,
    necessidadesEducacionais: null,
    adaptacoes: null,
    orientacoesProfessores: null,
    recursosApoio: null,
    cuidadosPedagogicos: null,
    vigenciaInicio: "2026-02-01",
    vigenciaFim: "2026-07-31",
  },
  hashConteudo: null,
  assinaturas: [],
  ciencias: [],
  publicadaEm: null,
  publicadaPor: null,
  acoes: acoesNenhuma,
  versao: "e1",
  foraDaVigencia: false,
};

/** Auxiliar de testes: monta a árvore com sessão, query client, toast e roteador. */
export function montar(ui: ReactElement, perfil: string) {
  const sessao: ContextoSessao = {
    estado: "autenticado",
    usuario: { id: "u1", nome: "Helena Prado", email: "h@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false },
    saiuPorVontade: false,
    entrar: vi.fn(),
    trocarSenha: vi.fn(),
    sair: vi.fn(),
  };
  return render(
    <StrictMode>
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <SessaoContexto value={sessao}>
        <ToastProvider>
          <MemoryRouter>{ui}</MemoryRouter>
        </ToastProvider>
      </SessaoContexto>
    </QueryClientProvider>
    </StrictMode>,
  );
}
