import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { useResponderMensagem } from "../hooks/useComunicacoes";

const esquema = z.object({ corpo: z.string().trim().min(1, "Escreva a resposta.").max(10000, "Use no máximo 10000 caracteres.") });
type Valores = z.infer<typeof esquema>;

/** Resposta no mesmo fio, para o outro participante (assunto "Re: …" vem da API). */
export function ResponderMensagem({ mensagemId, aoConcluir, aoCancelar }: { mensagemId: string; aoConcluir: () => void; aoCancelar: () => void }) {
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const responder = useResponderMensagem();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({ resolver: zodResolver(esquema), defaultValues: { corpo: "" } });

  async function enviar(d: Valores) {
    setErroGeral(null);
    try {
      await responder.mutateAsync({ id: mensagemId, corpo: d.corpo, chave });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["corpo"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-3">
      {erroGeral && <Alert variante="urgente">{erroGeral}</Alert>}
      <Field rotulo="Resposta" erro={errors.corpo?.message} obrigatorio>
        {/* O botão "Responder" some ao abrir o formulário: o foco segue para o campo em vez de cair no body. */}
        <Textarea autoFocus maxLength={10000} className="min-h-32" {...register("corpo")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Cancelar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Enviar resposta
        </Button>
      </div>
    </form>
  );
}
