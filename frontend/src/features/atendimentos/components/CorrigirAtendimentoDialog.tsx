import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useId, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { diaCivil, hojeCivil, horaCivil, instanteCivil } from "../../../lib/format";
import type { AtendimentoDetalhe } from "../../../lib/types";
import { camposHorario, CAMPO_DA_API, ListasHorario } from "../../agenda/components/CompromissoForm";
import { ROTULO_TIPO_ATENDIMENTO } from "../../agenda/components/estiloCompromisso";
import { useCorrigirAtendimento } from "../hooks/useAtendimentos";
import { entre } from "../../../lib/entre";
import { CamposClinicos, EscolhaEstado, textoClinico, TIPOS_ATENDIMENTO } from "./AtendimentoForm";

const ESTADOS = ["Realizado", "Ausente", "Remarcado", "Cancelado"] as const;

function criarEsquema(exigeJustificativa: boolean) {
  return z
    .object({
      estado: z.enum(ESTADOS, "Selecione o estado."),
      tipo: z.string(),
      ...camposHorario,
      demandaPrincipal: textoClinico,
      observacoesPrivadas: textoClinico,
      recomendacoesEncaminhamento: textoClinico,
      proximosPassos: textoClinico,
      justificativa: z
        .string()
        .trim()
        .transform((v) => (v === "" ? null : v)),
    })
    .superRefine((v, ctx) => {
      if (!entre(TIPOS_ATENDIMENTO, v.tipo)) ctx.addIssue({ code: "custom", path: ["tipo"], message: "Selecione o tipo." });
      if (v.estado === "Realizado" && v.demandaPrincipal == null)
        ctx.addIssue({ code: "custom", path: ["demandaPrincipal"], message: "Informe a demanda principal." });
      if (v.fim <= v.inicio) ctx.addIssue({ code: "custom", path: ["fim"], message: "O horário de término deve ser posterior ao de início." });
      if (exigeJustificativa && v.justificativa == null)
        ctx.addIssue({ code: "custom", path: ["justificativa"], message: "Informe a justificativa da correção." });
      else if (v.justificativa != null && (v.justificativa.length < 5 || v.justificativa.length > 500))
        ctx.addIssue({ code: "custom", path: ["justificativa"], message: "A justificativa deve ter de 5 a 500 caracteres." });
    });
}
type Esquema = ReturnType<typeof criarEsquema>;
type Entrada = z.input<Esquema>;
type Saida = z.output<Esquema>;
const CAMPOS: (keyof Entrada)[] = ["estado", "tipo", "data", "inicio", "fim", "demandaPrincipal", "observacoesPrivadas", "recomendacoesEncaminhamento", "proximosPassos", "justificativa"];

/**
 * Correção de um atendimento registrado. Quem não é o profissional do atendimento (a coordenação) precisa justificar;
 * a API audita e guarda a versão anterior.
 */
export function CorrigirAtendimentoDialog({
  atendimento,
  usuarioId,
  aoConcluir,
  aoCancelar,
}: {
  atendimento: AtendimentoDetalhe;
  usuarioId: string;
  aoConcluir: () => void;
  aoCancelar: () => void;
}) {
  const exigeJustificativa = usuarioId !== atendimento.profissionalId;
  const [esquema] = useState(() => criarEsquema(exigeJustificativa));
  // Snapshot do mount, como os defaults: após um 409 a versão refetchada não bate com os campos ainda velhos.
  const [versao] = useState(atendimento.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const corrigir = useCorrigirAtendimento();
  const idHorarios = useId();
  const dados = atendimento.dadosClinicos;
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: {
      estado: atendimento.estado,
      tipo: atendimento.tipo,
      data: diaCivil(atendimento.inicio),
      inicio: horaCivil(atendimento.inicio),
      fim: horaCivil(atendimento.fim),
      demandaPrincipal: dados?.demandaPrincipal ?? "",
      observacoesPrivadas: dados?.observacoesPrivadas ?? "",
      recomendacoesEncaminhamento: dados?.recomendacoesEncaminhamento ?? "",
      proximosPassos: dados?.proximosPassos ?? "",
      justificativa: "",
    },
  });

  // Aberto ao trocar de modo dentro do mesmo Modal: o foco vai para o primeiro campo.
  useEffect(() => setFocus(exigeJustificativa ? "justificativa" : "tipo"), [setFocus, exigeJustificativa]);

  async function enviar(d: Saida) {
    setErroGeral(null);
    const tipo = entre(TIPOS_ATENDIMENTO, d.tipo);
    if (!tipo) return;
    try {
      await corrigir.mutateAsync({
        id: atendimento.id,
        versao,
        body: {
          estado: d.estado,
          tipo,
          inicio: instanteCivil(d.data, d.inicio),
          fim: instanteCivil(d.data, d.fim),
          demandaPrincipal: d.demandaPrincipal,
          observacoesPrivadas: d.observacoesPrivadas,
          recomendacoesEncaminhamento: d.recomendacoesEncaminhamento,
          proximosPassos: d.proximosPassos,
          justificativa: d.justificativa,
        },
      });
      aoConcluir();
    } catch (e) {
      // 409 (conflito de horário) e 422 vão para o Alert; o formulário mantém o que foi digitado.
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message }), CAMPO_DA_API));
    }
  }

  return (
    // animate-surgir: o formulário substitui o detalhe dentro do mesmo Modal, como um painel remontado.
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex animate-surgir flex-col gap-4">
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      {exigeJustificativa && (
        <Field rotulo="Justificativa da correção" erro={errors.justificativa?.message} dica="Correções de outro profissional são justificadas e auditadas (de 5 a 500 caracteres)." obrigatorio>
          <Textarea {...register("justificativa")} />
        </Field>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <EscolhaEstado opcoes={ESTADOS} registro={register("estado")} erro={errors.estado?.message} />
        <Field rotulo="Tipo" erro={errors.tipo?.message} obrigatorio>
          <Select {...register("tipo")}>
            {TIPOS_ATENDIMENTO.map((t) => (
              <option key={t} value={t}>
                {ROTULO_TIPO_ATENDIMENTO[t]}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field rotulo="Data" erro={errors.data?.message} obrigatorio>
          <Input type="date" max={hojeCivil()} {...register("data")} />
        </Field>
        <Field rotulo="Início" erro={errors.inicio?.message} obrigatorio>
          <Input inputMode="numeric" placeholder="00:00" list={`${idHorarios}-inicio`} autoComplete="off" {...register("inicio")} />
        </Field>
        <Field rotulo="Fim" erro={errors.fim?.message} obrigatorio>
          <Input inputMode="numeric" placeholder="00:00" list={`${idHorarios}-fim`} autoComplete="off" {...register("fim")} />
        </Field>
        <ListasHorario id={idHorarios} />
      </div>
      {/* Na correção os dados clínicos ficam visíveis em qualquer estado: a API guarda o que vier e só Realizado exige demanda. */}
      <CamposClinicos register={register} errors={errors} demandaObrigatoria={watch("estado") === "Realizado"} />
      {!exigeJustificativa && (
        <Field rotulo="Justificativa (opcional)" erro={errors.justificativa?.message}>
          <Textarea {...register("justificativa")} />
        </Field>
      )}
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
