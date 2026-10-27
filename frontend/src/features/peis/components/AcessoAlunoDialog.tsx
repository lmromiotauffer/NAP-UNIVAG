import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Modal } from "../../../components/ui/Modal";
import { useToast } from "../../../components/ui/Toast";
import { mensagemErro } from "../../../lib/api";
import { formatarDataHora } from "../../../lib/format";
import { useGerarAcessoAluno } from "../hooks/usePeis";
import { INSTRUCAO_ACESSO } from "../rotulos";

/**
 * Gera o acesso temporário do aluno sob demanda (gerar invalida o anterior). Link e código vivem só neste estado de mutação (sem cache de query, sem localStorage)
 * e somem no `reset()` ao fechar. Montar só enquanto aberto.
 */
export function AcessoAlunoDialog({ versaoId, aoFechar }: { versaoId: string; aoFechar: () => void }) {
  const toast = useToast();
  const gerar = useGerarAcessoAluno();
  const { reset } = gerar;

  function fechar() {
    reset();
    aoFechar();
  }

  async function copiar(link: string) {
    try {
      await navigator.clipboard.writeText(link);
      toast.sucesso("Link copiado.");
    } catch {
      toast.erro("Não foi possível copiar. Selecione o link e copie manualmente.");
    }
  }

  const acesso = gerar.data;
  const link = acesso ? `${window.location.origin}/assinar/${acesso.token}` : "";
  return (
    <Modal aberto aoFechar={fechar} titulo="Acesso do aluno para assinar">
      <div className="flex flex-col gap-4">
        {gerar.isError && <Alert variante="urgente" className="animate-surgir">{mensagemErro(gerar.error)}</Alert>}
        {!acesso && (
          <>
            <p className="m-0 text-sm text-suave">Gerar um novo link invalida o anterior.</p>
            <div>
              <Button carregando={gerar.isPending} onClick={() => gerar.mutate(versaoId)}>
                Gerar link e código
              </Button>
            </div>
          </>
        )}
        {acesso && (
          // Resultado da ação no lugar do botão: chega com o surgir, como o Alert de erro.
          <div className="flex animate-surgir flex-col gap-4">
            <p className="m-0 text-sm">{INSTRUCAO_ACESSO}</p>
            <div className="flex flex-col gap-1">
              <span className="text-sm font-medium">Link</span>
              <div className="flex items-center gap-2">
                <code className="min-w-0 flex-1 rounded-md bg-hachura px-2 py-1.5 text-rotulo break-all">{link}</code>
                <Button tamanho="sm" variante="secundaria" className="flex-none" onClick={() => void copiar(link)}>
                  Copiar link
                </Button>
              </div>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-sm font-medium">Código</span>
              <strong className="font-titulo text-[2rem] tracking-[0.3em] tabular-nums">
                {acesso.codigo}
              </strong>
            </div>
            <p className="m-0 text-xs text-suave">Válido até {formatarDataHora(acesso.expiraEm)}.</p>
          </div>
        )}
        <div className="flex justify-end">
          <Button variante="secundaria" onClick={fechar}>
            Fechar
          </Button>
        </div>
      </div>
    </Modal>
  );
}
