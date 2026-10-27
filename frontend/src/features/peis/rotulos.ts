import type { VarianteSemantica } from "../../components/ui/variantes";
import type { components } from "../../lib/api-schema";

type EstadoVersaoPei = components["schemas"]["EstadoVersaoPei"];

export const ROTULO_ESTADO: Record<EstadoVersaoPei, string> = {
  Rascunho: "Rascunho",
  EmRevisao: "Em revisão",
  AguardandoAssinaturas: "Aguardando assinaturas",
  Assinada: "Assinada",
  Publicada: "Publicada",
  Arquivada: "Arquivada",
};

export const VARIANTE_ESTADO: Record<EstadoVersaoPei, VarianteSemantica> = {
  Rascunho: "neutro",
  EmRevisao: "aguardando",
  AguardandoAssinaturas: "aguardando",
  Assinada: "info",
  Publicada: "realizado",
  Arquivada: "neutro",
};

export const AVISO_ASSINATURA = "Assinatura demonstrativa — não certificada (pendência institucional)";
export const TEXTO_CIENCIA = "Registrar ciência confirma que a coordenação leu esta versão. Não é assinatura.";
export const INSTRUCAO_ACESSO = "Entregue o link e o código ao aluno pessoalmente ou por canais separados. Eles aparecem só agora e valem por 72 horas.";

export const ROTULO_PAPEL = { Profissional: "Profissional", Aluno: "Aluno" } as const;

export const CAMPOS_PEDAGOGICOS = [
  ["necessidadesEducacionais", "Necessidades educacionais"],
  ["adaptacoes", "Adaptações"],
  ["orientacoesProfessores", "Orientações aos professores"],
  ["recursosApoio", "Recursos de apoio"],
  ["cuidadosPedagogicos", "Cuidados pedagógicos"],
] as const;
