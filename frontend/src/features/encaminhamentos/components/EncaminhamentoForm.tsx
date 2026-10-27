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
import { hojeCivil } from "../../../lib/format";
import { BuscaAluno, type AlunoEscolhido } from "../../alunos/components/BuscaAluno";
import { useCriarEncaminhamento } from "../hooks/useEncaminhamentos";
import { DESTINOS_CLINICOS, ESPECIALIDADES, notaMotivoRestrito, ROTULO_DESTINO, ROTULO_ESPECIALIDADE } from "../rotulos";

const esquema = z
  .object({
    alunoId: z.string().min(1, "Selecione o aluno."),
    especialidade: z.enum(ESPECIALIDADES, "Selecione a especialidade."),
    // String + checagem no refine: o destino depende da especialidade.
    destino: z.string().optional(),
    data: z.iso.date("Informe a data do encaminhamento."),
    motivoRestrito: z.string().trim().min(3, "Informe o motivo restrito (mínimo 3 caracteres).").max(4000, "Use no máximo 4000 caracteres."),
    resumoCompartilhavel: z.string().trim().min(3, "Informe o resumo compartilhável (mínimo 3 caracteres).").max(2000, "Use no máximo 2000 caracteres."),
    observacoes: z
      .string()
      .trim()
      .max(4000, "Use no máximo 4000 caracteres.")
      .transform((v) => (v === "" ? null : v)),
  })
  // when: sem ele o Zod 4 pula o refine enquanto houver erro de campo e o destino só apareceria no 2º envio.
  .refine((v) => v.especialidade === "CoordenacaoCurso" || (DESTINOS_CLINICOS as readonly (string | undefined)[]).includes(v.destino), {
    path: ["destino"],
    message: "Selecione o destino.",
    when: () => true,
  })
  .refine((v) => !(v.data > hojeCivil()), { path: ["data"], message: "A data não pode estar no futuro.", when: () => true });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS: (keyof Entrada)[] = ["alunoId", "especialidade", "destino", "data", "motivoRestrito", "resumoCompartilhavel", "observacoes"];

/** Novo encaminhamento (Equipe NAP). Com `aluno`, o aluno vem fixo (aberto pela ficha). */
export function EncaminhamentoForm({ aluno: alunoFixo, aoConcluir, aoCancelar }: { aluno?: AlunoEscolhido; aoConcluir: (id: string) => void; aoCancelar?: () => void }) {
  // Uma chave por formulário: retentativas reaproveitam a mesma.
  const [chave] = useState(() => crypto.randomUUID());
  const [hoje] = useState(hojeCivil);
  const [aluno, setAluno] = useState<AlunoEscolhido | null>(alunoFixo ?? null);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const criar = useCriarEncaminhamento();
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    clearErrors,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: { alunoId: alunoFixo?.id ?? "", especialidade: "" as Entrada["especialidade"], destino: "", data: hoje, motivoRestrito: "", resumoCompartilhavel: "", observacoes: "" },
  });
  const coordenacao = watch("especialidade") === "CoordenacaoCurso";

  // Coordenação de curso tem destino fixo; ao sair dela o destino volta a ser escolhido.
  useEffect(() => {
    if (coordenacao) setValue("destino", "CoordenacaoCurso");
    else if (watch("destino") === "CoordenacaoCurso") setValue("destino", "");
  }, [coordenacao, setValue, watch]);

  async function enviar(d: Saida) {
    setErroGeral(null);
    // O select desabilitado não entra nos valores do formulário: o destino da coordenação é derivado aqui.
    const destino = d.especialidade === "CoordenacaoCurso" ? "CoordenacaoCurso" : (d.destino as (typeof DESTINOS_CLINICOS)[number]);
    try {
      const { id } = await criar.mutateAsync({ body: { ...d, destino }, chave });
      aoConcluir(id);
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      <p className="m-0 text-xs text-suave">Use encaminhamento quando outro setor precisa agir; orientação interna é um atendimento.</p>
      {alunoFixo ? (
        <div className="flex flex-col gap-1">
          <span className="text-sm font-medium text-texto">Aluno</span>
          <div className="rounded-md border border-borda bg-realce px-3 py-2 text-sm break-words">
            <span className="font-semibold">{alunoFixo.nome}</span>
            {alunoFixo.matricula && <span className="text-suave"> · {alunoFixo.matricula}</span>}
          </div>
        </div>
      ) : (
        <BuscaAluno
          aluno={aluno}
          aoEscolher={(a) => {
            setAluno(a);
            setValue("alunoId", a?.id ?? "");
            if (a) clearErrors("alunoId");
          }}
          erro={errors.alunoId?.message}
          obrigatorio
        />
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Especialidade" erro={errors.especialidade?.message} obrigatorio>
          <Select {...register("especialidade")}>
            <option value="">Selecione</option>
            {ESPECIALIDADES.map((e) => (
              <option key={e} value={e}>
                {ROTULO_ESPECIALIDADE[e]}
              </option>
            ))}
          </Select>
        </Field>
        <Field rotulo="Destino" erro={errors.destino?.message} dica={coordenacao ? "Definido pela especialidade." : undefined} obrigatorio>
          <Select disabled={coordenacao} {...register("destino")}>
            {coordenacao ? (
              <option value="CoordenacaoCurso">{ROTULO_DESTINO.CoordenacaoCurso}</option>
            ) : (
              <>
                <option value="">Selecione</option>
                {DESTINOS_CLINICOS.map((d) => (
                  <option key={d} value={d}>
                    {ROTULO_DESTINO[d]}
                  </option>
                ))}
              </>
            )}
          </Select>
        </Field>
      </div>
      <Field rotulo="Data do encaminhamento" erro={errors.data?.message} obrigatorio>
        <Input type="date" max={hoje} className="sm:max-w-[200px]" {...register("data")} />
      </Field>
      <Field rotulo="Motivo restrito" erro={errors.motivoRestrito?.message} dica={notaMotivoRestrito(watch("destino"))} obrigatorio>
        <Textarea maxLength={4000} {...register("motivoRestrito")} />
      </Field>
      <Field rotulo="Resumo compartilhável" erro={errors.resumoCompartilhavel?.message} dica="Lido pelo destino; a coordenação do curso só o vê quando é o destino." obrigatorio>
        <Textarea maxLength={2000} {...register("resumoCompartilhavel")} />
      </Field>
      <Field rotulo="Observações (opcional)" erro={errors.observacoes?.message} dica="Visíveis só à equipe NAP.">
        <Textarea maxLength={4000} {...register("observacoes")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        {aoCancelar && (
          <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
            Cancelar
          </Button>
        )}
        <Button type="submit" carregando={isSubmitting}>
          Encaminhar
        </Button>
      </div>
    </form>
  );
}
