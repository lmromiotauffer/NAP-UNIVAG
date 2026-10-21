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
import { tratarErro } from "../../../lib/api";
import { formatarTamanho } from "../../../lib/format";
import { useAnexarDocumento } from "../hooks/useDocumentos";
import { AVISO_RESTRITO, CATEGORIAS, EXTENSOES_ACEITAS, LIMITE_BYTES, ROTULO_CATEGORIA } from "../rotulos";

const esquema = z.object({
  arquivo: z
    .custom<FileList>()
    .refine((l) => l?.length > 0, "Selecione o arquivo.")
    .refine((l) => !l?.[0] || l[0].size <= LIMITE_BYTES, `O arquivo excede o limite de ${formatarTamanho(LIMITE_BYTES)}.`),
  categoria: z.enum(CATEGORIAS, "Selecione a categoria."),
  descricao: z.string().trim().min(3, "Informe a descrição (de 3 a 500 caracteres).").max(500, "Informe a descrição (de 3 a 500 caracteres)."),
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS = ["arquivo", "categoria", "descricao"] as const;

/** Anexar documento (Equipe NAP). Montar só enquanto aberto: a chave de idempotência vale por abertura. */
export function AnexarDocumentoDialog({ alunoId, aoFechar, aoConcluir }: { alunoId: string; aoFechar: () => void; aoConcluir: () => void }) {
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const anexar = useAnexarDocumento();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { categoria: "" as Entrada["categoria"], descricao: "" } });

  async function enviar(d: Saida) {
    setErroGeral(null);
    const form = new FormData();
    form.append("arquivo", d.arquivo[0] as File);
    form.append("categoria", d.categoria);
    form.append("descricao", d.descricao);
    try {
      await anexar.mutateAsync({ alunoId, form, chave });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Anexar documento">
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        <p className="m-0 rounded-xl bg-realce px-3.5 py-3 text-rotulo text-suave">{AVISO_RESTRITO}</p>
        <Field rotulo="Arquivo" erro={errors.arquivo?.message} dica={`PDF, PNG, JPG, DOCX, XLSX, TXT ou CSV, até ${formatarTamanho(LIMITE_BYTES)}.`} obrigatorio>
          <Input type="file" accept={EXTENSOES_ACEITAS} {...register("arquivo")} />
        </Field>
        <Field rotulo="Categoria" erro={errors.categoria?.message} obrigatorio>
          <Select {...register("categoria")}>
            <option value="">Selecione</option>
            {CATEGORIAS.map((c) => (
              <option key={c} value={c}>
                {ROTULO_CATEGORIA[c]}
              </option>
            ))}
          </Select>
        </Field>
        <Field rotulo="Descrição" erro={errors.descricao?.message} obrigatorio>
          <Textarea maxLength={500} {...register("descricao")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Cancelar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            Anexar
          </Button>
        </div>
      </form>
    </Modal>
  );
}
