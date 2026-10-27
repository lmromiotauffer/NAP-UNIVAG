import { mensagemErro } from "../../lib/api";
import { cn } from "../../lib/cn";
import { Alert } from "./Alert";
import { Button } from "./Button";

export function ErrorState({ erro, aoTentarNovamente, className }: { erro: unknown; aoTentarNovamente?: () => void; className?: string }) {
  return (
    // Sem animação de entrada por padrão: costuma substituir um skeleton já visível (ver EmptyState).
    // Quando é inserido sobre conteúdo já na tela (refetch falho), quem chama passa `className="animate-surgir"`.
    <Alert variante="urgente" className={cn("flex flex-col items-start gap-2", className)}>
      <p>{mensagemErro(erro)}</p>
      {aoTentarNovamente && (
        <Button tamanho="sm" variante="secundaria" onClick={aoTentarNovamente}>
          Tentar novamente
        </Button>
      )}
    </Alert>
  );
}
