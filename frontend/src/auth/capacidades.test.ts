import { describe, expect, it } from "vitest";
import type { UsuarioSessao } from "../lib/types";
import { ehCoordenacao, MATRIZ, pode } from "./capacidades";

const usuario = (perfil: string): UsuarioSessao => ({ id: "1", nome: "Teste", email: "t@nap.demo", perfil, cursoId: null, curso: null, deveTrocarSenha: false });

describe("capacidades", () => {
  it("matriz_coordenador_de_curso_nao_tem_agenda", () => {
    expect(pode(usuario("CoordenadorCurso"), "agenda")).toBe(false);
    expect(pode(usuario("CoordenadorCurso"), "alunos.consultar")).toBe(true);
    expect(pode(usuario("CoordenadorCurso"), "alunos.gerenciar")).toBe(false);
  });

  it("Gestao_nao_ve_comunicacoes", () => {
    expect(MATRIZ.Gestao).toEqual(["painel", "producao"]);
    expect(pode(usuario("Gestao"), "comunicacoes")).toBe(false);
    expect(pode(usuario("Gestao"), "alunos.consultar")).toBe(false);
    for (const perfil of ["Administrador", "CoordenadorNap", "ProfissionalNap", "CoordenadorCurso", "Sip"]) expect(pode(usuario(perfil), "comunicacoes")).toBe(true);
  });

  it("Capacidades_do_sip_incluem_encaminhamentos_mas_nao_alunos", () => {
    expect(pode(usuario("Sip"), "encaminhamentos")).toBe(true);
    expect(pode(usuario("Sip"), "encaminhamentos.criar")).toBe(false);
    expect(pode(usuario("Sip"), "alunos.consultar")).toBe(false);
  });

  it("encaminhamentos_criar_so_equipe_nap_e_coordenacao_de_curso_so_consulta", () => {
    expect(pode(usuario("ProfissionalNap"), "encaminhamentos.criar")).toBe(true);
    expect(pode(usuario("CoordenadorCurso"), "encaminhamentos")).toBe(true);
    expect(pode(usuario("CoordenadorCurso"), "encaminhamentos.criar")).toBe(false);
    expect(pode(usuario("CoordenadorCurso"), "documentos.anexar")).toBe(true);
    expect(pode(usuario("CoordenadorCurso"), "documentos.gerenciar")).toBe(false);
    for (const perfil of ["Sip", "Gestao"]) expect(pode(usuario(perfil), "documentos.anexar")).toBe(false);
    expect(pode(usuario("Gestao"), "encaminhamentos")).toBe(false);
  });

  it("peis_so_equipe_nap_e_coordenador_de_curso_e_publicar_so_coordenacao", () => {
    for (const perfil of ["Administrador", "CoordenadorNap", "ProfissionalNap", "CoordenadorCurso"]) expect(pode(usuario(perfil), "peis")).toBe(true);
    for (const perfil of ["Sip", "Gestao"]) expect(pode(usuario(perfil), "peis")).toBe(false);
    expect(pode(usuario("Administrador"), "peis.publicar")).toBe(true);
    expect(pode(usuario("CoordenadorNap"), "peis.publicar")).toBe(true);
    expect(pode(usuario("ProfissionalNap"), "peis.publicar")).toBe(false);
    expect(pode(usuario("CoordenadorCurso"), "peis.publicar")).toBe(false);
  });

  it("atividades_consulta_equipe_nap_e_coordenacao_de_curso_e_registro_so_equipe_nap", () => {
    for (const perfil of ["Administrador", "CoordenadorNap", "ProfissionalNap", "CoordenadorCurso"]) expect(pode(usuario(perfil), "atividades")).toBe(true);
    for (const perfil of ["Sip", "Gestao"]) expect(pode(usuario(perfil), "atividades")).toBe(false);
    for (const perfil of ["Administrador", "CoordenadorNap", "ProfissionalNap"]) expect(pode(usuario(perfil), "atividades.registrar")).toBe(true);
    for (const perfil of ["CoordenadorCurso", "Sip", "Gestao"]) expect(pode(usuario(perfil), "atividades.registrar")).toBe(false);
  });

  it("administracao_so_administrador_e_coordenacao_nap", () => {
    for (const perfil of ["Administrador", "CoordenadorNap"]) expect(pode(usuario(perfil), "administracao")).toBe(true);
    for (const perfil of ["ProfissionalNap", "CoordenadorCurso", "Sip", "Gestao"]) expect(pode(usuario(perfil), "administracao")).toBe(false);
  });

  it("perfil_desconhecido_ou_sem_usuario_nao_pode_nada", () => {
    expect(pode(usuario("Visitante"), "painel")).toBe(false);
    expect(pode(null, "painel")).toBe(false);
  });

  it("coordenacao_e_administrador_e_coordenador_nap", () => {
    expect(ehCoordenacao(usuario("Administrador"))).toBe(true);
    expect(ehCoordenacao(usuario("CoordenadorNap"))).toBe(true);
    expect(ehCoordenacao(usuario("ProfissionalNap"))).toBe(false);
    expect(ehCoordenacao(null)).toBe(false);
  });

  it("producao_consulta_equipe_gestao_e_coordenacao_de_curso_e_gerir_so_coordenacao_nap", () => {
    for (const perfil of ["Administrador", "CoordenadorNap", "ProfissionalNap", "CoordenadorCurso", "Gestao"]) expect(pode(usuario(perfil), "producao")).toBe(true);
    expect(pode(usuario("Sip"), "producao")).toBe(false);
    for (const perfil of ["Administrador", "CoordenadorNap"]) expect(pode(usuario(perfil), "producao.gerir")).toBe(true);
    for (const perfil of ["ProfissionalNap", "CoordenadorCurso", "Gestao", "Sip"]) expect(pode(usuario(perfil), "producao.gerir")).toBe(false);
  });
});
