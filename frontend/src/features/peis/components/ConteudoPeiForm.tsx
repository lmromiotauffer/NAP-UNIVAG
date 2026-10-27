import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { pode } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Textarea } from "../../../components/ui/Textarea";
import { normalizarErro, tratarErro } from "../../../lib/api";
import type { VersaoPeiDetalhe } from "../../../lib/types";
import { useAcaoVersao, useSalvarConteudo } from "../hooks/usePeis";
import { CAMPOS_PEDAGOGICOS } from "../rotulos";

const texto = z
  .string()
  .trim()
  .max(4000, "Use no máximo 4000 caracteres.")
  .transform((v) => (v === "" ? null : v));

const esquema = z
  .object({
    demandaInterna: texto,
    necessidadesEducacionais: texto,
    adaptacoes: texto,
    orientacoesProfessores: texto,
    recursosApoio: texto,
    cuidadosPedagogicos: texto,
    vigenciaInicio: z.iso.date("Informe o início da vigência."),
    vigenciaFim: z.iso.date("Informe o fim da vigência."),
  })
  .refine((v) => v.vigenciaFim >= v.vigenciaInicio, { path: ["vigenciaFim"], message: "O fim da vigência não pode ser anterior ao início.", when: () => true });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS: (keyof Entrada)[] = ["demandaInterna", ...CAMPOS_PEDAGOGICOS.map(([c]) => c), "vigenciaInicio", "vigenciaFim"];

/** Conteúdo da versão em Rascunho. A versão (If-Match) é o snapshot da abertura; só avança após salvar com sucesso.
 * Após um 409, `aoConflito` relê a versão e remonta o formulário com os valores do servidor. */
export function ConteudoPeiForm({ versao: dto, aoEnviado, aoConflito }: { versao: VersaoPeiDetalhe; aoEnviado?: () => void; aoConflito?: () => void }) {
  const { usuario } = useSessao();
  const [versao, setVersao] = useState(dto.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const [salvo, setSalvo] = useState(false);
  const [conflito, setConflito] = useState(false);
  // Qual botão disparou o envio: `isSubmitting` é um só para os dois, e o spinner tem de ficar no botão pressionado.
  const [enviando, setEnviando] = useState(false);
  const salvar = useSalvarConteudo();
  const acao = useAcaoVersao();
  const c = dto.conteudo;
  // Equipe NAP edita a demanda interna mesmo vazia; a coordenação de curso nunca a recebe.
  const comDemanda = c.demandaInterna != null || pode(usuario, "alunos.gerenciar");
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: {
      demandaInterna: c.demandaInterna ?? "",
      necessidadesEducacionais: c.necessidadesEducacionais ?? "",
      adaptacoes: c.adaptacoes ?? "",
      orientacoesProfessores: c.orientacoesProfessores ?? "",
      recursosApoio: c.recursosApoio ?? "",
      cuidadosPedagogicos: c.cuidadosPedagogicos ?? "",
      vigenciaInicio: c.vigenciaInicio,
      vigenciaFim: c.vigenciaFim,
    },
  });

  async function gravar(d: Saida) {
    // A próxima ação parte da versão que o PUT gravou (reler aqui pegaria a de outra aba e passaria por cima dela).
    const nova = await salvar.mutateAsync({ id: dto.id, versao, body: { ...d, demandaInterna: comDemanda ? d.demandaInterna : c.demandaInterna } });
    setVersao(nova);
    return nova;
  }

  async function aoSalvar(d: Saida) {
    setErroGeral(null);
    setSalvo(false);
    setConflito(false);
    try {
      await gravar(d);
      setSalvo(true);
    } catch (e) {
      setConflito(normalizarErro(e).status === 409);
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  async function aoEnviar(d: Saida) {
    setErroGeral(null);
    setSalvo(false);
    setConflito(false);
    if (!CAMPOS_PEDAGOGICOS.some(([campo]) => d[campo])) {
      setErroGeral("Preencha ao menos um dos campos de conteúdo pedagógico para enviar à revisão.");
      return;
    }
    setEnviando(true);
    try {
      const nova = await gravar(d);
      await acao.mutateAsync({ acao: "enviarRevisao", id: dto.id, versao: nova });
      aoEnviado?.();
    } catch (e) {
      setConflito(normalizarErro(e).status === 409);
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(aoSalvar)} className="flex flex-col gap-4" aria-label="Conteúdo do PEI">
      {erroGeral && (
        <Alert variante="urgente" className="flex animate-surgir flex-col items-start gap-2">
          <span>{erroGeral}</span>
          {conflito && aoConflito && (
            <>
              <span>Recarregar traz o conteúdo gravado agora e substitui o texto não salvo deste formulário.</span>
              <Button tamanho="sm" variante="secundaria" onClick={aoConflito}>
                Recarregar valores atuais
              </Button>
            </>
          )}
        </Alert>
      )}
      {salvo && (
        <Alert variante="realizado" className="animate-surgir" role="status">
          Rascunho salvo.
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2 sm:max-w-md">
        <Field rotulo="Início da vigência" erro={errors.vigenciaInicio?.message} obrigatorio>
          <Input type="date" {...register("vigenciaInicio")} />
        </Field>
        <Field rotulo="Fim da vigência" erro={errors.vigenciaFim?.message} obrigatorio>
          <Input type="date" {...register("vigenciaFim")} />
        </Field>
      </div>
      {CAMPOS_PEDAGOGICOS.map(([campo, rotulo]) => (
        <Field key={campo} rotulo={rotulo} erro={errors[campo]?.message}>
          <Textarea maxLength={4000} {...register(campo)} />
        </Field>
      ))}
      {comDemanda && (
        <Field rotulo="Demanda interna" dica="Uso interno do NAP — não faz parte do documento assinado." erro={errors.demandaInterna?.message}>
          <Textarea maxLength={4000} {...register("demandaInterna")} />
        </Field>
      )}
      <div className="flex flex-wrap justify-end gap-2">
        <Button type="submit" variante="secundaria" carregando={isSubmitting && !enviando} disabled={isSubmitting}>
          Salvar rascunho
        </Button>
        {dto.acoes.podeEnviarRevisao && (
          <Button onClick={() => void handleSubmit(aoEnviar)()} carregando={enviando} disabled={isSubmitting}>
            Enviar para revisão
          </Button>
        )}
      </div>
    </form>
  );
}
