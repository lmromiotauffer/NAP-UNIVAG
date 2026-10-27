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
import { useToast } from "../../../components/ui/Toast";
import { tratarErro } from "../../../lib/api";
import type { AlunoFicha } from "../../../lib/types";
import { useAlterarSituacao } from "../hooks/useAlunos";
import { ROTULO_SITUACAO, SITUACOES } from "../rotulos";

const esquema = z.object({
  situacao: z.enum(SITUACOES, "Selecione a nova situação."),
  motivo: z.string().trim().min(3, "Informe o motivo (mínimo 3 caracteres).").max(500, "Use no máximo 500 caracteres."),
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;

export function AlterarSituacaoDialog({ aluno, aberto, aoFechar }: { aluno: AlunoFicha; aberto: boolean; aoFechar: () => void }) {
  const toast = useToast();
  const alterar = useAlterarSituacao();
  // Versão da abertura (If-Match): o diálogo só monta aberto; após um 409, fechar e reabrir parte da ficha relida.
  const [versao] = useState(aluno.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { motivo: "" } });

  function fechar() {
    reset();
    setErroGeral(null);
    aoFechar();
  }

  async function enviar(body: Saida) {
    setErroGeral(null);
    try {
      await alterar.mutateAsync({ id: aluno.id, body, versao });
      toast.sucesso("Situação alterada.");
      fechar();
    } catch (e) {
      setErroGeral(tratarErro(e, ["situacao", "motivo"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <Modal aberto={aberto} aoFechar={fechar} titulo="Alterar situação" bloqueado={isSubmitting}>
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        <p className="text-sm text-suave">
          Situação atual: <strong className="font-semibold text-texto">{aluno.situacao ? ROTULO_SITUACAO[aluno.situacao] : "—"}</strong>
        </p>
        <Field rotulo="Nova situação" erro={errors.situacao?.message} obrigatorio>
          <Select defaultValue="" {...register("situacao")}>
            <option value="" disabled>
              Selecione
            </option>
            {SITUACOES.filter((s) => s !== aluno.situacao).map((s) => (
              <option key={s} value={s}>
                {ROTULO_SITUACAO[s]}
              </option>
            ))}
          </Select>
        </Field>
        <Field rotulo="Motivo" erro={errors.motivo?.message} dica="Aparece na linha do tempo do aluno." obrigatorio>
          <Textarea maxLength={500} {...register("motivo")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={fechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            Alterar situação
          </Button>
        </div>
      </form>
    </Modal>
  );
}
