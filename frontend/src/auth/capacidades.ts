/*
 * Espelha docs/permissoes.md e backend/src/Nap.Api/Infra/Politicas.cs. Só esconde UI: quem decide é a API.
 *
 * Política backend            → capacidade
 * PodeVerPainel               → "painel"                 (todos os seis perfis)
 * PodeConsultarAlunos         → "alunos.consultar"       (Equipe NAP + CoordenadorCurso)
 * PodeGerenciarAlunos         → "alunos.gerenciar"       (Equipe NAP)
 * PodeUsarAgenda              → "agenda"                 (Equipe NAP)
 * PodeRegistrarAtendimentos   → "atendimentos.registrar" (Equipe NAP)
 * PodeCorrigirAtendimentos    → "atendimentos.corrigir"  (Equipe NAP; própria × justificada fica no handler)
 * PodeConsultarAtividades    → "atividades"            (Equipe NAP + CoordenadorCurso; a API recorta por perfil)
 * PodeRegistrarAtividades    → "atividades.registrar"  (Equipe NAP; própria × coordenação fica no handler)
 * PodeUsarComunicacoes      → "comunicacoes"           (todos exceto Gestao; inclui a aba Notificações)
 * PodeConsultarEncaminhamentos → "encaminhamentos"       (Equipe NAP + Sip + CoordenadorCurso)
 * EquipeNap (POST encaminhamentos) → "encaminhamentos.criar" (Equipe NAP)
 * EquipeNap (escrita de documentos)  → "documentos.gerenciar"  (Equipe NAP; ler a aba segue "alunos.consultar")
 * PodeConsultarPeis          → "peis"                   (Equipe NAP + CoordenadorCurso; a API recorta por perfil)
 * PodePublicarPei            → "peis.publicar"          (Administrador + CoordenadorNap: aprovar revisão, devolver, publicar)
 * PodeConsultarProducao      → "producao"               (Equipe NAP + Gestao + CoordenadorCurso; a API recorta por perfil)
 * PodeGerirProducao           → "producao.gerir"         (Administrador + CoordenadorNap: reabrir semana e ajustes)
 * PodeAdministrar           → "administracao"          (Administrador + CoordenadorNap; só o Administrador gerencia administradores, isso fica no handler)
 * Retorno e cancelamento não viram capacidade: a tela segue `podeRegistrarRetorno`/`podeCancelar` do DTO.
 *
 * Equipe NAP = Administrador, CoordenadorNap, ProfissionalNap.
 */
import type { UsuarioSessao } from "../lib/types";

type Perfil = "Administrador" | "CoordenadorNap" | "ProfissionalNap" | "CoordenadorCurso" | "Sip" | "Gestao";

export type Capacidade =
  | "painel"
  | "comunicacoes"
  | "alunos.consultar"
  | "alunos.gerenciar"
  | "agenda"
  | "atendimentos.registrar"
  | "atendimentos.corrigir"
  | "atividades"
  | "atividades.registrar"
  | "encaminhamentos"
  | "encaminhamentos.criar"
  | "documentos.gerenciar"
  | "peis"
  | "peis.publicar"
  | "producao"
  | "producao.gerir"
  | "administracao";

const EQUIPE_NAP: Capacidade[] = [
  "painel",
  "comunicacoes",
  "alunos.consultar",
  "alunos.gerenciar",
  "agenda",
  "atendimentos.registrar",
  "atendimentos.corrigir",
  "atividades",
  "atividades.registrar",
  "encaminhamentos",
  "encaminhamentos.criar",
  "documentos.gerenciar",
  "peis",
  "producao",
];

export const MATRIZ: Record<Perfil, Capacidade[]> = {
  Administrador: [...EQUIPE_NAP, "peis.publicar", "producao.gerir", "administracao"],
  CoordenadorNap: [...EQUIPE_NAP, "peis.publicar", "producao.gerir", "administracao"],
  ProfissionalNap: EQUIPE_NAP,
  CoordenadorCurso: ["painel", "comunicacoes", "alunos.consultar", "atividades", "encaminhamentos", "peis", "producao"],
  Sip: ["painel", "comunicacoes", "encaminhamentos"],
  Gestao: ["painel", "producao"],
};

const ROTULO_PERFIL: Record<Perfil, string> = {
  Administrador: "Administração",
  CoordenadorNap: "Coordenação NAP",
  ProfissionalNap: "Profissional NAP",
  CoordenadorCurso: "Coordenação de curso",
  Sip: "SIP",
  Gestao: "Gestão",
};

const ehPerfil = (p: string): p is Perfil => Object.hasOwn(MATRIZ, p);

export function pode(usuario: UsuarioSessao | null, cap: Capacidade): boolean {
  return !!usuario && ehPerfil(usuario.perfil) && MATRIZ[usuario.perfil].includes(cap);
}

/** Espelha EhCoordenacao do backend: escolhe o profissional e age sobre a agenda de toda a equipe. */
export function ehCoordenacao(usuario: UsuarioSessao | null): boolean {
  return usuario?.perfil === "Administrador" || usuario?.perfil === "CoordenadorNap";
}

export function rotuloPerfil(perfil: string): string {
  return ehPerfil(perfil) ? ROTULO_PERFIL[perfil] : perfil;
}
