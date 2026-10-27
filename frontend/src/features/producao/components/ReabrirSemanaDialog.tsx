import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { formatarDataCivil, somarDias } from "../../../lib/format";
import type { SemanaProducao } from "../../../lib/types";
import { useReabrirSemana } from "../hooks/useProducao";

const esquema = z.object({ justificativa: z.string().trim().min(5, "Informe a justificativa (mínimo 5 caracteres).").max(500, "A justificativa deve ter no máximo 500 caracteres.") });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS = ["justificativa"] as const;

/** Formulário de reabertura (vai dentro de um Modal). A versão enviada no If-Match é a do fechamento ao abrir. */
export function ReabrirSemanaDialog({ semana, aoConcluir, aoCancelar }: { semana: SemanaProducao; aoConcluir: () => void; aoCancelar: () => void }) {
  const [versao] = useState(semana.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const reabrir = useReabrirSemana();
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { justificativa: "" } });

  useEffect(() => setFocus("justificativa"), [setFocus]);

  async function enviar(d: Saida) {
    setErroGeral(null);
    if (!versao) {
      setErroGeral("Esta semana não tem fechamento para reabrir. Atualize a página.");
      return;
    }
    try {
      await reabrir.mutateAsync({ body: { profissionalId: semana.profissionalId, semana: semana.semana, justificativa: d.justificativa }, versao });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      <p className="text-sm text-suave">
        Semana de {formatarDataCivil(semana.semana)} a {formatarDataCivil(somarDias(semana.semana, 6))}, {semana.profissional}. A reabertura fica registrada no histórico.
      </p>
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      <Field rotulo="Justificativa" erro={errors.justificativa?.message} obrigatorio>
        <Textarea maxLength={500} {...register("justificativa")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Voltar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Reabrir semana
        </Button>
      </div>
    </form>
  );
}
