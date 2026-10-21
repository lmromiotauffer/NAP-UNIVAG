import { TriangleAlert } from "lucide-react";
import { Button } from "../ui/Button";
import { EmptyState } from "../ui/EmptyState";

/** `errorElement` das rotas: exceção de render ou chunk que sumiu após um deploy. Nunca mostra o stack trace. `telaCheia` fora do AppShell. */
export function ErroRota({ telaCheia }: { telaCheia?: boolean }) {
  return (
    <div className={telaCheia ? "flex min-h-dvh items-center justify-center bg-fundo text-texto" : undefined}>
      <title>Erro ao abrir a página · NAP</title>
      <EmptyState
        nivel="h1"
        icone={<TriangleAlert />}
        titulo="Não foi possível abrir esta página"
        descricao="Algo deu errado ou o sistema foi atualizado enquanto você o usava. Recarregue a página; se o problema continuar, volte ao início."
        acao={
          <div className="mt-2 flex flex-wrap items-center justify-center gap-3">
            <Button onClick={() => window.location.reload()}>Recarregar a página</Button>
            {/* <a> e não <Link>: recarrega o sistema inteiro, inclusive o chunk que falhou. */}
            <a href="/" className="text-sm font-semibold text-primaria underline-offset-2 hover:underline">
              Ir para o início
            </a>
          </div>
        }
      />
    </div>
  );
}
