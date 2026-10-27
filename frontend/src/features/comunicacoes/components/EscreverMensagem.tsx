import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { useController, useForm } from "react-hook-form";
import { z } from "zod";
import { pode } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import type { MensagemDetalhe } from "../../../lib/types";
import { BuscaAluno, type AlunoEscolhido } from "../../alunos/components/BuscaAluno";
import { useAtualizarRascunho, useCriarRascunho, useDestinatarios, useEnviarMensagem } from "../hooks/useComunicacoes";
import { AVISO_COORDENACAO, ROTULO_TIPO, SEM_DESTINATARIOS, tiposOferecidos, type TipoMensagem } from "../rotulos";

const TIPOS = Object.keys(ROTULO_TIPO) as [TipoMensagem, ...TipoMensagem[]];

// O destinatário só é exigido no envio: rascunho pode ficar sem ele.
const esquema = z.object({
  tipo: z.enum(TIPOS, "Selecione o tipo."),
  destinatarioId: z.string(),
  alunoId: z.string(),
  assunto: z.string().trim().min(3, "Informe o assunto (mínimo 3 caracteres).").max(200, "Use no máximo 200 caracteres."),
  corpo: z.string().trim().min(1, "Escreva a mensagem.").max(10000, "Use no máximo 10000 caracteres."),
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS: (keyof Entrada)[] = ["tipo", "destinatarioId", "alunoId", "assunto", "corpo"];

/**
 * Nova mensagem ou edição de rascunho. Vínculos: aluno (fixo quando aberto pela ficha ou pelo encaminhamento) e encaminhamento
 * desse aluno; sem vínculo de curso avulso (a API aceita cursoId na lista e no envio, mas o formulário não expõe o campo).
 */
export function EscreverMensagem({
  aluno: alunoFixo,
  encaminhamentoId: encaminhamentoFixo,
  rascunho,
  aoConcluir,
  aoCancelar,
}: {
  aluno?: AlunoEscolhido;
  encaminhamentoId?: string;
  rascunho?: MensagemDetalhe;
  aoConcluir: (acao: "enviada" | "rascunho", id: string) => void;
  aoCancelar?: () => void;
}) {
  const { usuario } = useSessao();
  // Uma chave por endpoint e por formulário: retentativas reaproveitam a mesma.
  const [chaveEnvio] = useState(() => crypto.randomUUID());
  const [chaveRascunho] = useState(() => crypto.randomUUID());
  // Snapshot da abertura (padrão das edições com If-Match).
  const [versao] = useState(rascunho?.versao);
  const encaminhamentoId = encaminhamentoFixo ?? rascunho?.encaminhamentoId ?? null;
  const alunoDoRascunho: AlunoEscolhido | null = rascunho?.alunoId ? { id: rascunho.alunoId, nome: rascunho.aluno ?? "" } : null;
  // Com encaminhamento vinculado o aluno não muda: o encaminhamento precisa pertencer a ele.
  const fixo = alunoFixo ?? (encaminhamentoId ? alunoDoRascunho : null);
  const [aluno, setAluno] = useState<AlunoEscolhido | null>(fixo ?? alunoDoRascunho);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const enviarMensagem = useEnviarMensagem();
  const criarRascunho = useCriarRascunho();
  const atualizarRascunho = useAtualizarRascunho();
  const destinatarios = useDestinatarios({ ...(aluno && { alunoId: aluno.id }), ...(encaminhamentoId && { encaminhamentoId }) });
  const {
    register,
    control,
    handleSubmit,
    setError,
    setValue,
    clearErrors,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({
    resolver: zodResolver(esquema),
    defaultValues: {
      tipo: rascunho?.tipo ?? ("" as Entrada["tipo"]),
      destinatarioId: rascunho?.destinatarioId ?? "",
      alunoId: aluno?.id ?? "",
      assunto: rascunho?.assunto ?? "",
      corpo: rascunho?.corpo ?? "",
    },
  });
  // Controlado (o Field precisa do Select como filho direto para ligar rótulo, dica e erro).
  const { field: campoDestinatario } = useController({ control, name: "destinatarioId" });
  const destinatarioId = watch("destinatarioId");
  const lista = destinatarios.data;

  // A lista chega depois do formulário (rascunho aberto, aluno trocado): o select é controlado para nunca guardar uma escolha
  // invisível, e um destinatário que saiu da lista é limpo para ser escolhido de novo.
  useEffect(() => {
    if (lista && destinatarioId && !lista.some((d) => d.id === destinatarioId)) setValue("destinatarioId", "");
  }, [lista, destinatarioId, setValue]);

  const escolhido = lista?.find((d) => d.id === destinatarioId);
  const vinculos = { cursoId: null, alunoId: aluno?.id ?? null, encaminhamentoId };
  const marcar = (campo: keyof Entrada, message: string) => setError(campo, { message });
  // Com outros campos inválidos o zod barra antes de `enviar`: o destinatário entra na mesma leva de erros.
  const exigirDestinatario = () => {
    if (!destinatarioId) marcar("destinatarioId", "Selecione o destinatário.");
  };

  async function enviar(d: Saida) {
    setErroGeral(null);
    if (!d.destinatarioId) return exigirDestinatario();
    try {
      const { id } = await enviarMensagem.mutateAsync({
        body: { rascunhoId: rascunho?.id ?? null, destinatarioId: d.destinatarioId, tipo: d.tipo, assunto: d.assunto, corpo: d.corpo, ...vinculos },
        chave: chaveEnvio,
        // Mesma versão do rascunho aberto: enviar de uma aba antiga não sobrescreve o que outra aba salvou.
        ...(rascunho && versao ? { versao } : {}),
      });
      aoConcluir("enviada", id);
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, marcar));
    }
  }

  async function salvar(d: Saida) {
    setErroGeral(null);
    const body = { tipo: d.tipo, assunto: d.assunto, corpo: d.corpo, destinatarioId: d.destinatarioId || null, ...vinculos };
    try {
      if (rascunho && versao) {
        await atualizarRascunho.mutateAsync({ id: rascunho.id, body, versao });
        aoConcluir("rascunho", rascunho.id);
      } else {
        const { id } = await criarRascunho.mutateAsync({ body, chave: chaveRascunho });
        aoConcluir("rascunho", id);
      }
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, marcar));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar, exigirDestinatario)} className="flex flex-col gap-4">
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      {fixo ? (
        <div className="flex flex-col gap-1">
          <span className="text-sm font-medium text-texto">Aluno</span>
          <div className="rounded-md border border-borda bg-realce px-3 py-2 text-sm break-words">
            <span className="font-semibold">{fixo.nome}</span>
            {fixo.matricula && <span className="text-suave"> · {fixo.matricula}</span>}
            {encaminhamentoId && <span className="text-suave"> · vinculada ao encaminhamento</span>}
          </div>
        </div>
      ) : (
        // O SIP não consulta alunos: escreve sem vínculo ou a partir do encaminhamento.
        pode(usuario, "alunos.consultar") && (
          <BuscaAluno
            aluno={aluno}
            aoEscolher={(a) => {
              setAluno(a);
              setValue("alunoId", a?.id ?? "");
              clearErrors("alunoId");
            }}
            erro={errors.alunoId?.message}
            obrigatorio={false}
          />
        )
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field rotulo="Tipo" erro={errors.tipo?.message} obrigatorio>
          <Select {...register("tipo")}>
            <option value="">Selecione</option>
            {tiposOferecidos(!!encaminhamentoId).map((t) => (
              <option key={t} value={t}>
                {ROTULO_TIPO[t]}
              </option>
            ))}
          </Select>
        </Field>
        <div className="flex flex-col gap-2">
          <Field rotulo="Destinatário" erro={errors.destinatarioId?.message} dica={lista?.length === 0 ? SEM_DESTINATARIOS : undefined} obrigatorio>
            {/* Enquanto a lista não chega o select fica desabilitado (e o Enviar também): nada é enviado sem estar à vista. */}
            <Select {...campoDestinatario} disabled={!lista}>
              <option value="">{destinatarios.isPending ? "Carregando…" : "Selecione"}</option>
              {lista?.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.nome}
                </option>
              ))}
            </Select>
          </Field>
          {/* Sem a lista não há como enviar: a falha vem com "Tentar novamente", como nas listagens. Entra animada porque chega abaixo de um select já visível, não no lugar de um skeleton. */}
          {destinatarios.isError && <ErrorState erro={destinatarios.error} aoTentarNovamente={() => void destinatarios.refetch()} className="animate-surgir" />}
        </div>
      </div>
      {/* O aviso responde à escolha do destinatário: chega como os demais avisos pós-ação. */}
      {escolhido?.perfil === "CoordenadorCurso" && (
        <Alert variante="aguardando" className="animate-surgir">
          {AVISO_COORDENACAO}
        </Alert>
      )}
      <Field rotulo="Assunto" erro={errors.assunto?.message} obrigatorio>
        <Input maxLength={200} {...register("assunto")} />
      </Field>
      <Field rotulo="Mensagem" erro={errors.corpo?.message} obrigatorio>
        <Textarea maxLength={10000} className="min-h-40" {...register("corpo")} />
      </Field>
      <div className="flex flex-wrap justify-end gap-2">
        {aoCancelar && (
          <Button variante="secundaria" onClick={aoCancelar} disabled={isSubmitting}>
            Cancelar
          </Button>
        )}
        {/* Cada botão mostra o próprio envio: isSubmitting cobre os dois e colocaria o spinner no Enviar ao salvar rascunho. */}
        <Button variante="secundaria" onClick={handleSubmit(salvar)} carregando={criarRascunho.isPending || atualizarRascunho.isPending} disabled={isSubmitting}>
          Salvar rascunho
        </Button>
        <Button type="submit" carregando={enviarMensagem.isPending} disabled={isSubmitting || !lista || lista.length === 0}>
          Enviar
        </Button>
      </div>
    </form>
  );
}
