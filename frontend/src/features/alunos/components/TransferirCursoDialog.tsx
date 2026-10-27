import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Modal } from "../../../components/ui/Modal";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { useToast } from "../../../components/ui/Toast";
import { tratarErro } from "../../../lib/api";
import { hojeCivil } from "../../../lib/format";
import type { AlunoFicha } from "../../../lib/types";
import { useCursos } from "../../compartilhado/catalogos";
import { useTransferirCurso } from "../hooks/useAlunos";

const esquema = z.object({
  novoCursoId: z.guid("Selecione o novo curso."),
  data: z.iso.date("Informe a data da transferência."),
  motivo: z.string().trim().min(3, "Informe o motivo (mínimo 3 caracteres).").max(500, "Use no máximo 500 caracteres."),
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS = ["novoCursoId", "data", "motivo"] as const;

export function TransferirCursoDialog({ aluno, aberto, aoFechar }: { aluno: AlunoFicha; aberto: boolean; aoFechar: () => void }) {
  const toast = useToast();
  const cursos = useCursos();
  const transferir = useTransferirCurso();
  // Versão da abertura (If-Match): o diálogo só monta aberto; após um 409, fechar e reabrir parte da ficha relida.
  const [versao] = useState(aluno.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const valoresIniciais = { novoCursoId: "", data: hojeCivil(), motivo: "" };
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: valoresIniciais });

  function fechar() {
    reset(valoresIniciais);
    setErroGeral(null);
    aoFechar();
  }

  async function enviar(body: Saida) {
    setErroGeral(null);
    try {
      await transferir.mutateAsync({ id: aluno.id, body, versao });
      toast.sucesso("Curso transferido.");
      fechar();
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  const opcoes = cursos.data?.filter((c) => c.ativo && c.id !== aluno.cursoId) ?? [];
  return (
    <Modal aberto={aberto} aoFechar={fechar} titulo="Transferir curso" bloqueado={isSubmitting}>
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        {cursos.isError && <Alert variante="urgente" className="animate-surgir">Não foi possível carregar os cursos. Feche e tente novamente.</Alert>}
        <p className="text-sm text-suave">
          Curso atual: <strong className="font-semibold text-texto">{aluno.curso}</strong>
        </p>
        <Field rotulo="Novo curso" erro={errors.novoCursoId?.message} obrigatorio>
          <Select {...register("novoCursoId")}>
            <option value="">{cursos.isPending ? "Carregando…" : "Selecione"}</option>
            {opcoes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field rotulo="Data da transferência" erro={errors.data?.message} obrigatorio>
          <Input type="date" {...register("data")} />
        </Field>
        <Field rotulo="Motivo" erro={errors.motivo?.message} obrigatorio>
          <Textarea maxLength={500} {...register("motivo")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={fechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            Transferir
          </Button>
        </div>
      </form>
    </Modal>
  );
}
