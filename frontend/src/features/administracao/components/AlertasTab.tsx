import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { classeSecao } from "../../../components/ui/Card";
import { Button } from "../../../components/ui/Button";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Skeleton } from "../../../components/ui/Skeleton";
import { useToast } from "../../../components/ui/Toast";
import { normalizarErro, tratarErro } from "../../../lib/api";
import { cn } from "../../../lib/cn";
import type { ConfiguracaoAlertas } from "../../../lib/types";
import { useAlertasConfig, useSalvarAlertas } from "../hooks/useAdministracao";

const LIMITES = [
  { campo: "faltasConsecutivas", rotulo: "Faltas consecutivas", min: 1, max: 20, dica: "Alerta quando o aluno falta a tantos atendimentos seguidos." },
  { campo: "faltasJanela", rotulo: "Faltas no período", min: 1, max: 50, dica: "Alerta quando o aluno acumula tantas faltas dentro do período abaixo." },
  { campo: "janelaDias", rotulo: "Período das faltas (dias)", min: 7, max: 365, dica: "Quantos dias para trás as faltas são contadas." },
  { campo: "diasSemAcolhimento", rotulo: "Dias sem acolhimento", min: 1, max: 90, dica: "Prazo para acolher um encaminhamento antes de ele aparecer como fora do prazo." },
  { campo: "diasSemAtualizacao", rotulo: "Dias sem atualização", min: 1, max: 180, dica: "Encaminhamento sem retorno por tantos dias aparece como sem atualização." },
  { campo: "diasPeiPendente", rotulo: "Dias com PEI pendente", min: 1, max: 180, dica: "PEI parado aguardando andamento por tantos dias vira pendência no painel." },
] as const;
type Campo = (typeof LIMITES)[number]["campo"];

// Cada grupo vira um cartão: o assunto dá o título e os campos ficam empilhados dentro dele.
const GRUPOS: { titulo: string; descricao: string; campos: Campo[] }[] = [
  { titulo: "Faltas", descricao: "Quando as faltas do aluno viram alerta no painel.", campos: ["faltasConsecutivas", "faltasJanela", "janelaDias"] },
  { titulo: "Encaminhamentos", descricao: "Prazos que marcam um encaminhamento como atrasado.", campos: ["diasSemAcolhimento", "diasSemAtualizacao"] },
  { titulo: "PEI", descricao: "Quando um PEI parado vira pendência.", campos: ["diasPeiPendente"] },
];

const inteiro = (min: number, max: number) => {
  const mensagem = `Informe um valor de ${min} a ${max}.`;
  return z.number(mensagem).int(mensagem).min(min, mensagem).max(max, mensagem);
};
const esquema = z.object(Object.fromEntries(LIMITES.map((l) => [l.campo, inteiro(l.min, l.max)])) as Record<Campo, ReturnType<typeof inteiro>>);
type Dados = z.infer<typeof esquema>;
const CAMPOS = LIMITES.map((l) => l.campo);

function FormAlertas({ dados, aoConflito }: { dados: ConfiguracaoAlertas; aoConflito: () => void }) {
  const toast = useToast();
  const salvar = useSalvarAlertas();
  // A versão do If-Match é a da abertura do formulário e só avança com o que este formulário salvou.
  const [versao, setVersao] = useState(dados.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const [conflito, setConflito] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Dados>({ resolver: zodResolver(esquema), defaultValues: Object.fromEntries(CAMPOS.map((c) => [c, dados[c]])) as Dados });

  async function enviar(body: Dados) {
    setErroGeral(null);
    setConflito(false);
    try {
      const salvo = await salvar.mutateAsync({ body, versao });
      setVersao(salvo.versao);
      toast.sucesso("Limites de alerta salvos.");
    } catch (e) {
      setConflito(normalizarErro(e).status === 409);
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
      <p className="m-0 text-sm text-suave">Os limites valem para o painel, os alertas e o prazo dos encaminhamentos.</p>
      {erroGeral && (
        <Alert variante="urgente" className="flex flex-wrap items-center justify-between gap-2">
          <span>{erroGeral}</span>
          {conflito && (
            <Button tamanho="sm" variante="secundaria" onClick={aoConflito}>
              Recarregar valores atuais
            </Button>
          )}
        </Alert>
      )}
      <div className="grid gap-4 lg:grid-cols-3">
        {GRUPOS.map((g) => (
          <section key={g.titulo} aria-labelledby={`alertas-${g.titulo}`} className={classeSecao}>
            <div>
              <h2 id={`alertas-${g.titulo}`} className="m-0 text-base font-semibold">
                {g.titulo}
              </h2>
              <p className="m-0 text-sm text-suave">{g.descricao}</p>
            </div>
            {LIMITES.filter((l) => g.campos.includes(l.campo)).map((l) => (
              <Field key={l.campo} rotulo={l.rotulo} dica={`${l.dica} De ${l.min} a ${l.max}.`} erro={errors[l.campo]?.message} obrigatorio>
                <Input type="number" inputMode="numeric" min={l.min} max={l.max} step={1} className="max-w-32" {...register(l.campo, { valueAsNumber: true })} />
              </Field>
            ))}
          </section>
        ))}
      </div>
      <div className="flex justify-end">
        <Button type="submit" carregando={isSubmitting}>
          Salvar limites
        </Button>
      </div>
    </form>
  );
}

export function AlertasTab() {
  const alertas = useAlertasConfig();
  // Remonta o formulário com os valores do servidor (depois de um 409).
  const [geracao, setGeracao] = useState(0);
  // Só o formulário que substitui o skeleton chega animado; vindo do cache ele monta junto com o painel da aba, que já anima.
  const [veioDoSkeleton] = useState(alertas.isPending);

  if (alertas.isError) return <ErrorState erro={alertas.error} aoTentarNovamente={() => void alertas.refetch()} />;
  if (!alertas.data) return <Skeleton className="h-96 rounded-secao" />;
  return (
    <div key={geracao} className={cn(veioDoSkeleton && "animate-surgir")}>
      <FormAlertas
        dados={alertas.data}
        aoConflito={() => {
          void alertas.refetch().then(() => setGeracao((g) => g + 1));
        }}
      />
    </div>
  );
}
