import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Modal } from "../../../components/ui/Modal";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import type { VersaoPeiDetalhe } from "../../../lib/types";
import { useProfissionais } from "../../compartilhado/catalogos";
import { useTrocarResponsavelPei } from "../hooks/usePeis";

const esquema = z.object({
  novoResponsavelId: z.string().min(1, "Escolha o novo responsável."),
  justificativa: z.string().trim().min(3, "Informe a justificativa (de 3 a 500 caracteres).").max(500, "Informe a justificativa (de 3 a 500 caracteres)."),
});
type Dados = z.infer<typeof esquema>;
const CAMPOS = ["novoResponsavelId", "justificativa"] as const;

/** Troca o responsável do PEI (todas as versões). Montar só enquanto aberto. */
export function TrocarResponsavelDialog({ versao, aoFechar, aoConcluir }: { versao: VersaoPeiDetalhe; aoFechar: () => void; aoConcluir: () => void }) {
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const profissionais = useProfissionais();
  const trocar = useTrocarResponsavelPei();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Dados>({ resolver: zodResolver(esquema), defaultValues: { novoResponsavelId: "", justificativa: "" } });

  async function enviar(body: Dados) {
    setErroGeral(null);
    try {
      await trocar.mutateAsync({ peiId: versao.peiId, body });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Trocar responsável">
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        <p className="m-0 text-sm text-suave">
          Responsável atual: {versao.responsavel}. As assinaturas já feitas permanecem; a assinatura profissional pendente passa ao novo responsável.
        </p>
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        {profissionais.isError && <Alert variante="urgente" className="animate-surgir">Não foi possível carregar os profissionais. Feche e tente novamente.</Alert>}
        <Field rotulo="Novo responsável" erro={errors.novoResponsavelId?.message} obrigatorio>
          <Select {...register("novoResponsavelId")}>
            <option value="">{profissionais.isPending ? "Carregando…" : "Selecione"}</option>
            {profissionais.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field rotulo="Justificativa" erro={errors.justificativa?.message} obrigatorio>
          <Textarea maxLength={500} {...register("justificativa")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            Trocar responsável
          </Button>
        </div>
      </form>
    </Modal>
  );
}
