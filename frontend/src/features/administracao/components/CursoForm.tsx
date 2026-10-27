import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { tratarErro } from "../../../lib/api";
import type { Curso } from "../../../lib/types";
import { useCriarCurso, useRenomearCurso } from "../hooks/useAdministracao";

const esquema = z.object({ nome: z.string().trim().min(2, "Informe o nome (mínimo 2 caracteres).").max(120, "Use no máximo 120 caracteres.") });
type Dados = z.infer<typeof esquema>;

export function CursoForm({ curso, aoConcluir, aoCancelar }: { curso?: Curso; aoConcluir: () => void; aoCancelar: () => void }) {
  // Uma chave por formulário e a versão do momento em que abriu: retentativas e refetch não as trocam.
  const [chave] = useState(() => crypto.randomUUID());
  const [versao] = useState(curso?.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const criar = useCriarCurso();
  const renomear = useRenomearCurso();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Dados>({ resolver: zodResolver(esquema), defaultValues: { nome: curso?.nome ?? "" } });

  async function enviar(body: Dados) {
    setErroGeral(null);
    try {
      if (curso) await renomear.mutateAsync({ id: curso.id, body, versao: versao ?? curso.versao });
      else await criar.mutateAsync({ body, chave });
      aoConcluir();
    } catch (e) {
      // 409 (nome já existe, versão antiga) e demais falhas vão para o Alert, mantendo o formulário.
      setErroGeral(tratarErro(e, ["nome"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      <Field rotulo="Nome do curso" erro={errors.nome?.message} obrigatorio>
        <Input autoComplete="off" {...register("nome")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Cancelar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Salvar
        </Button>
      </div>
    </form>
  );
}
