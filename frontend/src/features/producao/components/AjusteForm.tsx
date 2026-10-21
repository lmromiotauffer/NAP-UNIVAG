import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { DIA, hojeCivil } from "../../../lib/format";
import { useProfissionais } from "../../compartilhado/catalogos";
import { useCriarAjuste } from "../hooks/useProducao";
import { ROTULO_CATEGORIA_AJUSTE } from "../rotulos";

type CategoriaAjuste = keyof typeof ROTULO_CATEGORIA_AJUSTE;
const CATEGORIAS = Object.keys(ROTULO_CATEGORIA_AJUSTE) as CategoriaAjuste[];

/** Inteiro opcional (vazio = nulo), negativo permitido, dentro do intervalo da API. */
const inteiro = (min: number, max: number) =>
  z
    .string()
    .trim()
    .refine((v) => v === "" || /^-?\d+$/.test(v), "Informe um número inteiro.")
    .refine((v) => v === "" || (Number(v) >= min && Number(v) <= max), `Deve estar entre ${min} e ${max}.`)
    .transform((v) => (v === "" ? null : Number(v)));

const esquema = z
  .object({
    profissionalId: z.string().min(1, "Selecione o profissional."),
    data: z.string().regex(DIA, "Informe a data."),
    categoria: z.string().refine((v): v is CategoriaAjuste => (CATEGORIAS as string[]).includes(v), "Selecione a categoria."),
    ocorrencias: inteiro(-1000, 1000),
    minutos: inteiro(-10000, 10000),
    justificativa: z.string().trim().min(5, "Informe a justificativa (mínimo 5 caracteres).").max(500, "A justificativa deve ter no máximo 500 caracteres."),
  })
  .superRefine((d, ctx) => {
    if (!d.ocorrencias && !d.minutos) ctx.addIssue({ code: "custom", path: ["ocorrencias"], message: "Informe ocorrências ou minutos diferentes de zero." });
  });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS = ["profissionalId", "data", "categoria", "ocorrencias", "minutos", "justificativa"] as const;

/** Formulário de ajuste (vai dentro de um Modal). A Idempotency-Key nasce com o formulário: reenviar o mesmo envio não duplica. */
export function AjusteForm({ profissionalPadrao, aoConcluir, aoCancelar }: { profissionalPadrao?: string; aoConcluir: () => void; aoCancelar: () => void }) {
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const criar = useCriarAjuste();
  const profissionais = useProfissionais();
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: { profissionalId: profissionalPadrao ?? "", data: hojeCivil(), categoria: "", ocorrencias: "", minutos: "", justificativa: "" },
  });

  useEffect(() => setFocus("profissionalId"), [setFocus]);

  async function enviar(d: Saida) {
    setErroGeral(null);
    try {
      await criar.mutateAsync({
        body: { profissionalId: d.profissionalId, data: d.data, categoria: d.categoria as CategoriaAjuste, ocorrencias: d.ocorrencias, minutos: d.minutos, justificativa: d.justificativa },
        chave,
      });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente">{erroGeral}</Alert>}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Profissional" erro={errors.profissionalId?.message} obrigatorio>
          <Select {...register("profissionalId")}>
            <option value="">Selecione</option>
            {profissionais.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field rotulo="Data" erro={errors.data?.message} obrigatorio>
          <Input type="date" {...register("data")} />
        </Field>
      </div>
      <Field rotulo="Categoria" erro={errors.categoria?.message} obrigatorio>
        <Select {...register("categoria")}>
          <option value="">Selecione</option>
          {CATEGORIAS.map((c) => (
            <option key={c} value={c}>
              {ROTULO_CATEGORIA_AJUSTE[c]}
            </option>
          ))}
        </Select>
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Ocorrências" erro={errors.ocorrencias?.message} dica="Aceita negativos para descontar.">
          <Input type="number" step={1} inputMode="numeric" {...register("ocorrencias")} />
        </Field>
        <Field rotulo="Minutos" erro={errors.minutos?.message} dica="Aceita negativos para descontar.">
          <Input type="number" step={1} inputMode="numeric" {...register("minutos")} />
        </Field>
      </div>
      <Field rotulo="Justificativa" erro={errors.justificativa?.message} obrigatorio>
        <Textarea maxLength={500} {...register("justificativa")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Voltar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Registrar ajuste
        </Button>
      </div>
    </form>
  );
}
