import { mensagemErro } from "../../lib/api";
import { Alert } from "./Alert";
import { Button } from "./Button";

export function ErrorState({ erro, aoTentarNovamente }: { erro: unknown; aoTentarNovamente?: () => void }) {
  return (
    <Alert variante="urgente" className="flex flex-col items-start gap-2">
      <p>{mensagemErro(erro)}</p>
      {aoTentarNovamente && (
        <Button tamanho="sm" variante="secundaria" onClick={aoTentarNovamente}>
          Tentar novamente
        </Button>
      )}
    </Alert>
  );
}
