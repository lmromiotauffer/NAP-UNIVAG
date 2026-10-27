import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useId, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { normalizarErro, tratarErro } from "../../../lib/api";
import { diaCivil, formatarData, formatarIntervalo, horaCivil, instanteCivil } from "../../../lib/format";
import type { Compromisso, ConflitoAgenda } from "../../../lib/types";
import { useRemarcarCompromisso } from "../hooks/useAgenda";
import type { EscopoGrupo } from "../services/agendaService";
import { CAMPO_DA_API, camposHorario, ListasHorario, validarHorario } from "./CompromissoForm";
import { ListaConflitos } from "./ListaConflitos";

const esquema = z.object(camposHorario).superRefine(validarHorario);
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS = Object.keys(camposHorario) as (keyof Entrada)[];

/** Formulário de remarcação (vai dentro de um Modal; monte com `key` por compromisso para ter chave nova a cada abertura). */
export function RemarcarDialog({
  compromisso,
  escopo,
  aoConcluir,
  aoCancelar,
}: {
  compromisso: Compromisso;
  /** Compromisso de grupo: `Grupo` remarca todas as instâncias agendadas. */
  escopo?: EscopoGrupo;
  aoConcluir: (novoId: string, inicio: string) => void;
  aoCancelar: () => void;
}) {
  // Uma chave por formulário: um novo envio após 409 reaproveita a mesma.
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const [conflitos, setConflitos] = useState<ConflitoAgenda[]>([]);
  const remarcar = useRemarcarCompromisso();
  const idHorarios = useId();
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: { data: diaCivil(compromisso.inicio), inicio: horaCivil(compromisso.inicio), fim: horaCivil(compromisso.fim) },
  });

  // Aberto ao trocar de modo dentro do mesmo Modal: o foco vai para o primeiro campo.
  useEffect(() => setFocus("data"), [setFocus]);

  async function enviar(d: Saida) {
    setErroGeral(null);
    setConflitos([]);
    const inicio = instanteCivil(d.data, d.inicio);
    try {
      const { id } = await remarcar.mutateAsync({ id: compromisso.id, body: { inicio, fim: instanteCivil(d.data, d.fim) }, chave, versao: compromisso.versao, escopo });
      aoConcluir(id, inicio);
    } catch (e) {
      setConflitos(normalizarErro(e).conflitos ?? []);
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message }), CAMPO_DA_API));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      <p className="text-sm text-suave">
        Horário atual: {formatarData(compromisso.inicio)}, {formatarIntervalo(compromisso.inicio, compromisso.fim)}. {escopo === "Grupo"
          ? "As instâncias do grupo ficam marcadas como remarcadas e novas são criadas, para todos os participantes, no horário escolhido."
          : "O compromisso atual fica marcado como remarcado e um novo é criado no horário escolhido."}
      </p>
      {erroGeral && (
        <Alert variante="urgente" className="animate-surgir">
          {erroGeral}
          <ListaConflitos conflitos={conflitos} />
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-3">
        <Field rotulo="Nova data" erro={errors.data?.message} dica="Segunda a sexta-feira." obrigatorio>
          <Input type="date" {...register("data")} />
        </Field>
        <Field rotulo="Início" erro={errors.inicio?.message} obrigatorio>
          <Input inputMode="numeric" placeholder="00:00" list={`${idHorarios}-inicio`} autoComplete="off" {...register("inicio")} />
        </Field>
        <Field rotulo="Fim" erro={errors.fim?.message} obrigatorio>
          <Input inputMode="numeric" placeholder="00:00" list={`${idHorarios}-fim`} autoComplete="off" {...register("fim")} />
        </Field>
        <ListasHorario id={idHorarios} />
      </div>
      <div className="flex flex-wrap justify-end gap-2">
        <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
          Voltar
        </Button>
        <Button type="submit" carregando={isSubmitting}>
          Remarcar
        </Button>
      </div>
    </form>
  );
}
