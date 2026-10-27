import type { VarianteSemantica } from "../../components/ui/variantes";
import type { components } from "../../lib/api-schema";

export type Especialidade = components["schemas"]["Especialidade"];
export type Destino = components["schemas"]["DestinoEncaminhamento"];
type EstadoEncaminhamento = components["schemas"]["EstadoEncaminhamento"];
type AcaoRetorno = components["schemas"]["AcaoRetorno"];
/** O cancelamento tem operação própria (`/cancelar`). */
export type AcaoRegistravel = Exclude<AcaoRetorno, "Cancelamento">;

export const ROTULO_ESPECIALIDADE: Record<Especialidade, string> = {
  Psicologia: "Psicologia",
  Psiquiatria: "Psiquiatria",
  PsicologiaEPsiquiatria: "Psicologia e Psiquiatria",
  Neuropsicologia: "Neuropsicologia",
  CoordenacaoCurso: "Coordenação do curso",
};

export const ROTULO_DESTINO: Record<Destino, string> = {
  Sip: "SIP",
  ClinicaEscola: "Clínica Escola",
  Externo: "Serviço externo",
  CoordenacaoCurso: "Coordenação do curso",
};

export const ROTULO_ESTADO: Record<EstadoEncaminhamento, string> = {
  Encaminhado: "Encaminhado",
  Acolhido: "Acolhido",
  EmAtendimento: "Em atendimento",
  Alta: "Alta",
  Desistencia: "Desistência",
  Cancelado: "Cancelado",
};

// Âmbar = aguardando o serviço; azul = em acompanhamento; verde = alta; cinza = encerrado sem alta.
export const VARIANTE_ESTADO: Record<EstadoEncaminhamento, VarianteSemantica> = {
  Encaminhado: "aguardando",
  Acolhido: "info",
  EmAtendimento: "info",
  Alta: "realizado",
  Desistencia: "neutro",
  Cancelado: "neutro",
};

export const ROTULO_ACAO: Record<AcaoRetorno, string> = {
  Acolhimento: "Acolhimento",
  InicioAtendimento: "Início do atendimento",
  Evolucao: "Evolução",
  Alta: "Alta",
  Desistencia: "Desistência",
  Cancelamento: "Cancelamento",
};

export const BOTAO_ACAO: Record<AcaoRegistravel, string> = {
  Acolhimento: "Registrar acolhimento",
  InicioAtendimento: "Registrar início do atendimento",
  Evolucao: "Registrar evolução",
  Alta: "Registrar alta",
  Desistencia: "Registrar desistência",
};

export const ESPECIALIDADES = Object.keys(ROTULO_ESPECIALIDADE) as [Especialidade, ...Especialidade[]];
export const DESTINOS = Object.keys(ROTULO_DESTINO) as [Destino, ...Destino[]];
export const ESTADOS = Object.keys(ROTULO_ESTADO) as [EstadoEncaminhamento, ...EstadoEncaminhamento[]];
/** Coordenador de curso só recebe especialidade e destino quando o destino é a própria coordenação. */
export const NAO_COMPARTILHADO = "Não compartilhado com o curso";

export const especialidadeEDestino = (e: { especialidade: Especialidade | null; destino: Destino | null }): string =>
  e.especialidade && e.destino ? `${ROTULO_ESPECIALIDADE[e.especialidade]} → ${ROTULO_DESTINO[e.destino]}` : NAO_COMPARTILHADO;

/** Quem lê o motivo restrito: a equipe NAP e, só nos destinos SIP/Clínica Escola, o SIP. */
export function notaMotivoRestrito(destino: string | null | undefined): string {
  if (destino === "Sip" || destino === "ClinicaEscola") return "Visível só à equipe NAP e ao SIP/Clínica Escola.";
  if (destino) return "Visível só à equipe NAP.";
  return "Visível só à equipe NAP e, nos destinos SIP e Clínica Escola, ao serviço de destino.";
}

/** Destinos das especialidades clínicas; Coordenação de curso tem destino fixo. */
export const DESTINOS_CLINICOS = ["Sip", "ClinicaEscola", "Externo"] as const;

/** Retornos válidos em cada estado (a API também valida). Terminais não aceitam nenhum. */
export const ACOES_POR_ESTADO: Record<EstadoEncaminhamento, AcaoRegistravel[]> = {
  Encaminhado: ["Acolhimento", "Desistencia"],
  Acolhido: ["InicioAtendimento", "Evolucao", "Desistencia"],
  EmAtendimento: ["Evolucao", "Alta", "Desistencia"],
  Alta: [],
  Desistencia: [],
  Cancelado: [],
};
