import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Modal } from "../../../components/ui/Modal";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import { formatarDataCivil, hojeCivil } from "../../../lib/format";
import type { EncaminhamentoDetalhe } from "../../../lib/types";
import { useRegistrarRetorno } from "../hooks/useEncaminhamentos";
import { BOTAO_ACAO, type AcaoRegistravel } from "../rotulos";

const ROTULO_DATA: Record<Exclude<AcaoRegistravel, "Evolucao">, string> = {
  Acolhimento: "Data do acolhimento",
  InicioAtendimento: "Data do início do atendimento",
  Alta: "Data da alta",
  Desistencia: "Data da desistência",
};

/** Data mínima de cada retorno, como no domínio: a cadeia encaminhamento ≤ acolhimento ≤ início ≤ encerramento. */
function dataMinima(e: EncaminhamentoDetalhe, acao: AcaoRegistravel): string {
  if (acao === "InicioAtendimento") return e.dataAcolhimento ?? e.data;
  if (acao === "Alta") return e.dataPrimeiroAtendimento ?? e.data;
  if (acao === "Desistencia") return [e.data, e.dataAcolhimento, e.dataPrimeiroAtendimento].filter((d) => d != null).sort().at(-1) ?? e.data;
  return e.data;
}

function criarEsquema(acao: AcaoRegistravel, minima: string) {
  return z
    .object({
      data: z.string(),
      texto: z
        .string()
        .trim()
        .transform((v) => (v === "" ? null : v)),
    })
    .superRefine((v, ctx) => {
      if (acao !== "Evolucao") {
        if (!v.data) ctx.addIssue({ code: "custom", path: ["data"], message: "Informe a data." });
        else if (v.data > hojeCivil()) ctx.addIssue({ code: "custom", path: ["data"], message: "A data não pode estar no futuro." });
        else if (v.data < minima) ctx.addIssue({ code: "custom", path: ["data"], message: `A data não pode ser anterior a ${formatarDataCivil(minima)}.` });
      }
      const n = v.texto?.length ?? 0;
      if (acao === "Evolucao" && n === 0) ctx.addIssue({ code: "custom", path: ["texto"], message: "Descreva a evolução." });
      else if (acao === "Desistencia" && (n < 3 || n > 1000))
        ctx.addIssue({ code: "custom", path: ["texto"], message: "Informe a justificativa (de 3 a 1000 caracteres)." });
      else if (n > 4000) ctx.addIssue({ code: "custom", path: ["texto"], message: "Use no máximo 4000 caracteres." });
    });
}
type Esquema = ReturnType<typeof criarEsquema>;
type Entrada = z.input<Esquema>;
type Saida = z.output<Esquema>;

/** Registrar um retorno (acolhimento, início, evolução, alta ou desistência). Montar só enquanto aberto. */
export function RetornoDialog({
  encaminhamento,
  acao,
  aoFechar,
  aoConcluir,
}: {
  encaminhamento: EncaminhamentoDetalhe;
  acao: AcaoRegistravel;
  aoFechar: () => void;
  aoConcluir: () => void;
}) {
  const minima = dataMinima(encaminhamento, acao);
  const [esquema] = useState(() => criarEsquema(acao, minima));
  // Snapshot da abertura: após um 409 o detalhe refetchado traz versão nova, mas o que foi digitado partiu da antiga.
  const [versao] = useState(encaminhamento.versao);
  const [hoje] = useState(hojeCivil);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const registrar = useRegistrarRetorno();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { data: acao === "Evolucao" ? "" : hoje, texto: "" } });

  async function enviar(d: Saida) {
    setErroGeral(null);
    try {
      await registrar.mutateAsync({ id: encaminhamento.id, versao, body: { acao, data: acao === "Evolucao" ? null : d.data, texto: d.texto } });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["data", "texto"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  const visivelCoordenacao = encaminhamento.destino === "CoordenacaoCurso";
  const dicaTexto = visivelCoordenacao ? "Visível à coordenação do curso." : "Não aparece para a coordenação do curso.";
  return (
    <Modal aberto aoFechar={aoFechar} titulo={BOTAO_ACAO[acao]}>
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        <p className="text-sm text-suave">
          {encaminhamento.aluno} · {encaminhamento.curso}
        </p>
        {acao !== "Evolucao" && (
          <Field rotulo={ROTULO_DATA[acao]} erro={errors.data?.message} obrigatorio>
            <Input type="date" min={minima} max={hoje} className="sm:max-w-[200px]" {...register("data")} />
          </Field>
        )}
        {acao === "Evolucao" ? (
          <Field rotulo="Evolução" erro={errors.texto?.message} dica={dicaTexto} obrigatorio>
            <Textarea maxLength={4000} {...register("texto")} />
          </Field>
        ) : acao === "Desistencia" ? (
          <Field rotulo="Justificativa" erro={errors.texto?.message} dica={dicaTexto} obrigatorio>
            <Textarea maxLength={1000} {...register("texto")} />
          </Field>
        ) : (
          <Field rotulo="Observação (opcional)" erro={errors.texto?.message} dica={dicaTexto}>
            <Textarea maxLength={4000} {...register("texto")} />
          </Field>
        )}
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            {BOTAO_ACAO[acao]}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
