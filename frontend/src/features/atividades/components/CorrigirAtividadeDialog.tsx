import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { diaCivil, horaCivil, instanteCivil } from "../../../lib/format";
import type { AtividadeDetalhe } from "../../../lib/types";
import { CAMPO_DA_API } from "../../agenda/components/CompromissoForm";
import { useCorrigirAtividade } from "../hooks/useAtividades";
import { entre } from "../../../lib/entre";
import { TIPOS_ATIVIDADE } from "../rotulos";
import { CamposAtividade, verificarAtividade } from "./AtividadeForm";

const esquema = z
  .object({
    data: z.string(),
    inicio: z.string(),
    fim: z.string(),
    cursoId: z.string(),
    tipo: z.string(),
    descricao: z.string(),
    justificativa: z.string().trim(),
  })
  .superRefine((v, ctx) => {
    verificarAtividade(v, ctx);
    if (v.justificativa === "") ctx.addIssue({ code: "custom", path: ["justificativa"], message: "Informe a justificativa da correção." });
    else if (v.justificativa.length < 5 || v.justificativa.length > 500)
      ctx.addIssue({ code: "custom", path: ["justificativa"], message: "A justificativa deve ter de 5 a 500 caracteres." });
  });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS: (keyof Entrada)[] = ["data", "inicio", "fim", "cursoId", "tipo", "descricao", "justificativa"];

/** Correção de uma atividade registrada: toda correção é justificada, auditada e guarda a versão anterior. */
export function CorrigirAtividadeDialog({ atividade, aoConcluir, aoCancelar }: { atividade: AtividadeDetalhe; aoConcluir: () => void; aoCancelar: () => void }) {
  // Snapshot do mount, como os defaults: após um 409 a versão refetchada não bate com os campos ainda velhos.
  const [versao] = useState(atividade.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const corrigir = useCorrigirAtividade();
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: {
      data: diaCivil(atividade.inicio),
      inicio: horaCivil(atividade.inicio),
      fim: horaCivil(atividade.fim),
      cursoId: atividade.cursoId ?? "",
      tipo: atividade.tipo,
      descricao: atividade.descricao,
      justificativa: "",
    },
  });

  useEffect(() => setFocus("justificativa"), [setFocus]);

  async function enviar(d: Saida) {
    setErroGeral(null);
    const tipo = entre(TIPOS_ATIVIDADE, d.tipo);
    if (!tipo) return;
    try {
      await corrigir.mutateAsync({
        id: atividade.id,
        versao,
        body: {
          inicio: instanteCivil(d.data, d.inicio),
          fim: instanteCivil(d.data, d.fim),
          cursoId: d.cursoId || null,
          tipo,
          descricao: d.descricao.trim(),
          justificativa: d.justificativa,
        },
      });
      aoConcluir();
    } catch (e) {
      // 409 (versão antiga, conflito de horário) e 422 vão para o Alert; o formulário mantém o que foi digitado.
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message }), CAMPO_DA_API));
    }
  }

  return (
    // animate-surgir: o formulário substitui o detalhe dentro do mesmo Modal, como um painel remontado.
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex animate-surgir flex-col gap-4">
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      <Field rotulo="Justificativa da correção" erro={errors.justificativa?.message} dica="Toda correção é justificada e auditada (de 5 a 500 caracteres)." obrigatorio>
        <Textarea {...register("justificativa")} />
      </Field>
      <CamposAtividade register={register} errors={errors} cursoAtual={atividade.cursoId && atividade.curso ? { id: atividade.cursoId, nome: atividade.curso } : undefined} />
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Voltar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Salvar correção
        </Button>
      </div>
    </form>
  );
}
