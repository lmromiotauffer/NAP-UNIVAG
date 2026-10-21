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
import { useArquivarDocumento } from "../hooks/useDocumentos";

const esquema = z.object({
  motivo: z.string().trim().min(3, "Informe o motivo (de 3 a 500 caracteres).").max(500, "Informe o motivo (de 3 a 500 caracteres)."),
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;

/** Arquivar (Equipe NAP): o registro e o arquivo são preservados. Montar só enquanto aberto: a versão é a da abertura. */
export function ArquivarDocumentoDialog({ documento, aoFechar, aoConcluir }: { documento: Documento; aoFechar: () => void; aoConcluir: () => void }) {
  const [versao] = useState(documento.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const arquivar = useArquivarDocumento();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { motivo: "" } });

  async function enviar(body: Saida) {
    setErroGeral(null);
    try {
      await arquivar.mutateAsync({ id: documento.id, versao, body });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["motivo"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Arquivar documento">
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        <p className="m-0 text-sm">
          "{documento.descricao}" sai da lista comum. Só a coordenação do NAP ainda o consulta; o arquivo não é apagado.
        </p>
        <Field rotulo="Motivo" erro={errors.motivo?.message} obrigatorio>
          <Textarea maxLength={500} {...register("motivo")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" variante="perigo" carregando={isSubmitting}>
            Arquivar
          </Button>
        </div>
      </form>
    </Modal>
  );
}
