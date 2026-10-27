import { Check, Copy } from "lucide-react";
import { useState } from "react";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Dado } from "../../../components/ui/Dado";
import { Modal } from "../../../components/ui/Modal";

/**
 * Só exibe: a senha vive em `mutation.data` (gcTime 0) do dono do diálogo, que chama `reset()` ao fechar.
 * Nada vai para localStorage, URL ou toast.
 */
export function SenhaTemporariaDialog({ email, senha, aoFechar }: { email: string; senha: string | null; aoFechar: () => void }) {
  const [copiada, setCopiada] = useState(false);
  const [falhouCopia, setFalhouCopia] = useState(false);

  function fechar() {
    setCopiada(false);
    setFalhouCopia(false);
    aoFechar();
  }

  async function copiar() {
    if (!senha) return;
    try {
      await navigator.clipboard.writeText(senha);
      setCopiada(true);
      setFalhouCopia(false);
    } catch {
      setFalhouCopia(true);
    }
  }

  return (
    <Modal aberto={senha !== null} aoFechar={fechar} titulo="Senha temporária" bloqueado={false}>
      <div className="flex flex-col gap-4">
        <Alert variante="aguardando">Entregue esta senha pessoalmente. Ela aparece só agora e precisará ser trocada no primeiro acesso.</Alert>
        <dl className="flex flex-col gap-3 text-sm">
          <Dado rotulo="E-mail">{email}</Dado>
          {/* flex-wrap: em 320 px o botão desce de linha em vez de espremer a senha. */}
          <Dado rotulo="Senha temporária" classeValor="flex flex-wrap items-center gap-2">
            <code className="rounded-md bg-realce px-2.5 py-1.5 font-mono text-base tracking-wider select-all">{senha}</code>
            <Button tamanho="sm" variante="secundaria" onClick={() => void copiar()}>
              {copiada ? <Check aria-hidden="true" className="size-4" /> : <Copy aria-hidden="true" className="size-4" />}
              {copiada ? "Copiada" : "Copiar"}
            </Button>
          </Dado>
        </dl>
        {falhouCopia && (
          <p role="alert" className="animate-surgir text-xs text-urgente">
            Não foi possível copiar. Selecione a senha e copie manualmente.
          </p>
        )}
        <div className="flex justify-end">
          <Button onClick={fechar}>Concluir</Button>
        </div>
      </div>
    </Modal>
  );
}
