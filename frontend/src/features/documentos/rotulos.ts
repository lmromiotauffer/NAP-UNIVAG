import type { VarianteSemantica } from "../../components/ui/variantes";
import type { components } from "../../lib/api-schema";

export type CategoriaDocumento = components["schemas"]["CategoriaDocumento"];
export type VisibilidadeDocumento = components["schemas"]["VisibilidadeDocumento"];

export const ROTULO_CATEGORIA: Record<CategoriaDocumento, string> = {
  Laudo: "Laudo",
  Relatorio: "Relatório",
  Pei: "PEI",
  Termo: "Termo",
  Outro: "Outro",
};
export const CATEGORIAS = Object.keys(ROTULO_CATEGORIA) as CategoriaDocumento[];

export const ROTULO_VISIBILIDADE: Record<VisibilidadeDocumento, string> = {
  RestritoNap: "Restrito ao NAP",
  CompartilhadoCoordenacao: "Compartilhado com a coordenação",
};
export const VARIANTE_VISIBILIDADE: Record<VisibilidadeDocumento, VarianteSemantica> = {
  RestritoNap: "neutro",
  CompartilhadoCoordenacao: "info",
};

export const EXTENSOES_ACEITAS = ".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.txt,.csv";
// Só conveniência: quem decide o limite (Nap:Arquivos:LimiteBytes) é a API.
export const LIMITE_BYTES = 5 * 1024 * 1024;
export const AVISO_RESTRITO = "Documentos nascem restritos ao NAP. Compartilhar com a coordenação é uma ação separada.";
