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
import type { EncaminhamentoDetalhe } from "../../../lib/types";
import { useCancelarEncaminhamento } from "../hooks/useEncaminhamentos";

const esquema = z.object({
  justificativa: z.string().trim().min(3, "Informe a justificativa (de 3 a 1000 caracteres).").max(1000, "Informe a justificativa (de 3 a 1000 caracteres)."),
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;

/** Cancelamento (Equipe NAP). Montar só enquanto aberto: a versão é a da abertura. */
export function CancelarEncaminhamentoDialog({ encaminhamento, aoFechar, aoConcluir }: { encaminhamento: EncaminhamentoDetalhe; aoFechar: () => void; aoConcluir: () => void }) {
  const [versao] = useState(encaminhamento.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const cancelar = useCancelarEncaminhamento();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { justificativa: "" } });

  async function enviar(body: Saida) {
    setErroGeral(null);
    try {
      await cancelar.mutateAsync({ id: encaminhamento.id, versao, body });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["justificativa"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Cancelar encaminhamento">
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente">{erroGeral}</Alert>}
        <p className="text-sm">O encaminhamento de {encaminhamento.aluno} será encerrado e não poderá mais receber retornos.</p>
        <Field rotulo="Justificativa" erro={errors.justificativa?.message} obrigatorio>
          <Textarea maxLength={1000} {...register("justificativa")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" variante="perigo" carregando={isSubmitting}>
            Cancelar encaminhamento
          </Button>
        </div>
      </form>
    </Modal>
  );
}
