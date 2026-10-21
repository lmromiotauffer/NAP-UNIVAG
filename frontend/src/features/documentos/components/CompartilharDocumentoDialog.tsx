import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Modal } from "../../../components/ui/Modal";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import type { Documento } from "../../../lib/types";
import { useCompartilharDocumento } from "../hooks/useDocumentos";

const esquema = z.object({
  justificativa: z.string().trim().min(3, "Informe a justificativa (de 3 a 500 caracteres).").max(500, "Informe a justificativa (de 3 a 500 caracteres)."),
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;

/** Compartilha com a coordenação ou revoga o compartilhamento (Equipe NAP). Montar só enquanto aberto: a versão é a da abertura. */
export function CompartilharDocumentoDialog({ documento, revogar, aoFechar, aoConcluir }: { documento: Documento; revogar: boolean; aoFechar: () => void; aoConcluir: () => void }) {
  const [versao] = useState(documento.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const mutacao = useCompartilharDocumento(revogar);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { justificativa: "" } });

  async function enviar(body: Saida) {
    setErroGeral(null);
    try {
      await mutacao.mutateAsync({ id: documento.id, versao, body });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["justificativa"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  const acao = revogar ? "Revogar compartilhamento" : "Compartilhar";
  return (
    <Modal aberto aoFechar={aoFechar} titulo={revogar ? "Revogar compartilhamento" : "Compartilhar com a coordenação"}>
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        <p className="m-0 text-sm">
          {revogar
            ? `A coordenação do curso deixará de ver "${documento.descricao}".`
            : `A coordenação do curso passará a ver e baixar "${documento.descricao}". A justificativa fica registrada na auditoria.`}
        </p>
        <Field rotulo="Justificativa" erro={errors.justificativa?.message} obrigatorio>
          <Textarea maxLength={500} {...register("justificativa")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            {acao}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
