import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { formatarDataCivil } from "../../../lib/format";
import type { AlunoFicha } from "../../../lib/types";
import { useCursos, useProfissionais } from "../../compartilhado/catalogos";
import { useAtualizarAluno, useCriarAluno } from "../hooks/useAlunos";

const textoOpcional = z
  .string()
  .trim()
  .transform((v) => (v === "" ? null : v));

const esquema = z.object({
  nomeCompleto: z.string().trim().min(3, "Informe o nome completo (mínimo 3 caracteres).").max(200, "Use no máximo 200 caracteres."),
  matricula: z
    .string()
    .trim()
    .regex(/^[A-Za-z0-9.-]{3,20}$/, "A matrícula deve ter de 3 a 20 caracteres (letras, números, ponto ou hífen)."),
  cursoId: z.guid("Selecione o curso."),
  // Vazio é rejeitado antes da conversão: "" nunca vira 0.
  semestre: z
    .string()
    .trim()
    .min(1, "Informe o semestre.")
    .pipe(z.coerce.number<string>().int("Informe um número inteiro.").min(1, "O semestre vai de 1 a 20.").max(20, "O semestre vai de 1 a 20.")),
  inicioAcompanhamento: z.iso.date("Informe a data de início."),
  profissionalResponsavelId: z.guid("Selecione o profissional responsável."),
  necessidadesEducacionais: textoOpcional,
  observacoesAcademicas: textoOpcional,
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS = Object.keys(esquema.shape) as (keyof Entrada)[];
// Na edição estes campos não são exibidos: um erro neles vai para o Alert geral.
const SO_NA_CRIACAO: string[] = ["matricula", "cursoId", "inicioAcompanhamento"];

export function AlunoForm({
  aluno,
  aoConcluir,
  aoCancelar,
}: {
  aluno?: AlunoFicha;
  aoConcluir: (id: string) => void;
  aoCancelar?: () => void;
}) {
  const edicao = !!aluno;
  // Uma chave por formulário: retentativas reaproveitam a mesma (a API libera a chave quando a criação falha).
  const [chave] = useState(() => crypto.randomUUID());
  // Snapshot do mount, como os defaults: após um 409 a ficha refetchada traz versão nova, mas os campos continuam velhos.
  const [versao] = useState(aluno?.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const cursos = useCursos();
  const profissionais = useProfissionais();
  const criar = useCriarAluno();
  const atualizar = useAtualizarAluno();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: {
      nomeCompleto: aluno?.nome ?? "",
      matricula: aluno?.matricula ?? "",
      cursoId: aluno?.cursoId ?? "",
      semestre: aluno ? String(aluno.semestre) : "",
      inicioAcompanhamento: aluno?.inicioAcompanhamento ?? "",
      profissionalResponsavelId: aluno?.profissionalId ?? "",
      necessidadesEducacionais: aluno?.necessidadesEducacionais ?? "",
      observacoesAcademicas: aluno?.observacoesAcademicas ?? "",
    },
  });

  async function enviar(dados: Saida) {
    setErroGeral(null);
    try {
      if (aluno) {
        const { nomeCompleto, semestre, necessidadesEducacionais, observacoesAcademicas, profissionalResponsavelId } = dados;
        await atualizar.mutateAsync({
          id: aluno.id,
          body: { nomeCompleto, semestre, necessidadesEducacionais, observacoesAcademicas, profissionalResponsavelId },
          versao: versao ?? aluno.versao,
        });
        aoConcluir(aluno.id);
      } else {
        const { id } = await criar.mutateAsync({ body: dados, chave });
        aoConcluir(id);
      }
    } catch (e) {
      // 409 (matrícula já cadastrada ou chave em processamento), erros de campos não exibidos e falhas gerais: Alert.
      const visiveis = edicao ? CAMPOS.filter((c) => !SO_NA_CRIACAO.includes(c)) : CAMPOS;
      setErroGeral(tratarErro(e, visiveis, (campo, message) => setError(campo, { message })));
    }
  }

  const listaCursos = cursos.data?.filter((c) => c.ativo) ?? [];
  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      {(cursos.isError || profissionais.isError) && (
        <Alert variante="urgente" className="animate-surgir">Não foi possível carregar cursos ou profissionais. Feche e tente novamente.</Alert>
      )}
      <Field rotulo="Nome completo" erro={errors.nomeCompleto?.message} obrigatorio>
        <Input autoComplete="off" {...register("nomeCompleto")} />
      </Field>
      {edicao ? (
        <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
          <div>
            <dt className="text-xs text-suave">Matrícula</dt>
            <dd className="tabular-nums">{aluno.matricula}</dd>
          </div>
          <div>
            <dt className="text-xs text-suave">Curso</dt>
            <dd>{aluno.curso}</dd>
          </div>
          <div>
            <dt className="text-xs text-suave">Início do acompanhamento</dt>
            <dd>{formatarDataCivil(aluno.inicioAcompanhamento)}</dd>
          </div>
        </dl>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          <Field rotulo="Matrícula" erro={errors.matricula?.message} obrigatorio>
            <Input autoComplete="off" {...register("matricula")} />
          </Field>
          <Field rotulo="Curso" erro={errors.cursoId?.message} obrigatorio>
            <Select {...register("cursoId")}>
              <option value="">{cursos.isPending ? "Carregando…" : "Selecione"}</option>
              {listaCursos.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.nome}
                </option>
              ))}
            </Select>
          </Field>
        </div>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Semestre" erro={errors.semestre?.message} obrigatorio>
          <Input type="number" inputMode="numeric" min={1} max={20} {...register("semestre")} />
        </Field>
        {!edicao && (
          <Field rotulo="Início do acompanhamento" erro={errors.inicioAcompanhamento?.message} obrigatorio>
            <Input type="date" {...register("inicioAcompanhamento")} />
          </Field>
        )}
      </div>
      <Field rotulo="Profissional responsável" erro={errors.profissionalResponsavelId?.message} obrigatorio>
        <Select {...register("profissionalResponsavelId")}>
          <option value="">{profissionais.isPending ? "Carregando…" : "Selecione"}</option>
          {profissionais.data?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nome}
            </option>
          ))}
        </Select>
      </Field>
      <Field rotulo="Necessidades educacionais" erro={errors.necessidadesEducacionais?.message} dica="Visível à coordenação do curso. Não registre dados clínicos.">
        <Textarea {...register("necessidadesEducacionais")} />
      </Field>
      <Field rotulo="Observações acadêmicas" erro={errors.observacoesAcademicas?.message}>
        <Textarea {...register("observacoesAcademicas")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        {aoCancelar && (
          <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
            Cancelar
          </Button>
        )}
        <Button type="submit" carregando={isSubmitting}>
          Salvar
        </Button>
      </div>
    </form>
  );
}
