import { QueryClient } from "@tanstack/react-query";
import { normalizarErro } from "./lib/api";

/** Uma retentativa, só para falha de rede (status 0) ou do servidor (5xx): 4xx não muda ao repetir. */
export function deveRetentar(falhas: number, erro: unknown): boolean {
  const { status } = normalizarErro(erro);
  return falhas < 1 && (status === 0 || status >= 500);
}

export const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, retry: deveRetentar, refetchOnWindowFocus: false } },
});
