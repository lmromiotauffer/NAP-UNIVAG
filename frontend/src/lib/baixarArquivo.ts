import { isAxiosError } from "axios";
import { api, normalizarErro } from "./api";

function nomeDoCabecalho(cabecalho: unknown): string {
  if (typeof cabecalho !== "string") return "arquivo";
  const utf8 = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(cabecalho)?.[1];
  if (utf8) {
    try {
      return decodeURIComponent(utf8.trim());
    } catch {
      // Percent-encoding malformado: cai para o nome simples.
    }
  }
  return /filename\s*=\s*"?([^";]+)"?/i.exec(cabecalho)?.[1]?.trim() || "arquivo";
}

/** Com `responseType: "blob"` o ProblemDetails do erro chega como Blob: reabre o JSON para o normalizador comum. */
async function lerErroDoBlob(e: unknown): Promise<never> {
  if (isAxiosError(e) && e.response?.data instanceof Blob) {
    try {
      e.response.data = JSON.parse(await e.response.data.text());
    } catch {
      e.response.data = {};
    }
  }
  throw normalizarErro(e);
}

/** Baixa um arquivo autenticado (o token vai no cabeçalho, então não serve um link direto). */
export async function baixarArquivo(url: string): Promise<void> {
  // Sem timeout: arquivos e exportações grandes passam dos 30 s numa conexão lenta.
  const resposta = await api.get<Blob>(url, { responseType: "blob", timeout: 0 }).catch(lerErroDoBlob);
  const endereco = URL.createObjectURL(resposta.data);
  try {
    const a = document.createElement("a");
    a.href = endereco;
    a.download = nomeDoCabecalho(resposta.headers["content-disposition"]);
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    // Adia a revogação: alguns navegadores cancelam o download se a URL some antes de ele começar.
    setTimeout(() => URL.revokeObjectURL(endereco), 0);
  }
}
