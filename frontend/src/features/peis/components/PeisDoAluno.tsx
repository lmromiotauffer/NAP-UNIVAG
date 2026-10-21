import { zodResolver } from "@hookform/resolvers/zod";
import { NotebookPen } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";
import { z } from "zod";
import { pode } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Alert } from "../../../components/ui/Alert";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { classeSecao } from "../../../components/ui/Card";
import { Carregando } from "../../../components/ui/Carregando";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { Modal } from "../../../components/ui/Modal";
import { Select } from "../../../components/ui/Select";
import { Skeleton } from "../../../components/ui/Skeleton";
import { tratarErro } from "../../../lib/api";
import { formatarDataCivil, formatarDataHora } from "../../../lib/format";
import { useProfissionais } from "../../compartilhado/catalogos";
import { useCriarPei, useListaPeis } from "../hooks/usePeis";
import { ROTULO_ESTADO, VARIANTE_ESTADO } from "../rotulos";

const esquema = z
  .object({
    profissionalResponsavelId: z.string().min(1, "Selecione o profissional responsável."),
    vigenciaInicio: z.iso.date("Informe o início da vigência."),
    vigenciaFim: z.iso.date("Informe o fim da vigência."),
  })
  .refine((v) => v.vigenciaFim >= v.vigenciaInicio, { path: ["vigenciaFim"], message: "O fim da vigência não pode ser anterior ao início.", when: () => true });
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;
const CAMPOS = ["profissionalResponsavelId", "vigenciaInicio", "vigenciaFim"] as const;
// A API aninha a vigência em `conteudo.*`.
const MAPA_API = { "conteudo.vigenciaInicio": "vigenciaInicio", "conteudo.vigenciaFim": "vigenciaFim", ProfissionalResponsavelId: "profissionalResponsavelId" } as const;

/** Cria o PEI com o responsável e a vigência; o conteúdo se preenche na página da versão 1. */
function CriarPeiDialog({ alunoId, aoFechar }: { alunoId: string; aoFechar: () => void }) {
  const navegar = useNavigate();
  const profissionais = useProfissionais();
  const criar = useCriarPei();
  // Uma chave por abertura: retentativas reaproveitam a mesma.
  const [chave] = useState(() => crypto.randomUUID());
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { profissionalResponsavelId: "", vigenciaInicio: "", vigenciaFim: "" } });

  async function enviar(d: Saida) {
    setErroGeral(null);
    try {
      const { id } = await criar.mutateAsync({
        alunoId,
        chave,
        body: {
          profissionalResponsavelId: d.profissionalResponsavelId,
          conteudo: {
            demandaInterna: null,
            necessidadesEducacionais: null,
            adaptacoes: null,
            orientacoesProfessores: null,
            recursosApoio: null,
            cuidadosPedagogicos: null,
            vigenciaInicio: d.vigenciaInicio,
            vigenciaFim: d.vigenciaFim,
          },
        },
      });
      void navegar(`/peis/versoes/${id}`);
    } catch (e) {
      setErroGeral(tratarErro(e, CAMPOS, (campo, message) => setError(campo, { message }), MAPA_API));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Criar PEI">
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        {profissionais.isError && <Alert variante="urgente" className="animate-surgir">Não foi possível carregar os profissionais. Feche e tente novamente.</Alert>}
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
        <div className="grid gap-3 sm:grid-cols-2">
          <Field rotulo="Início da vigência" erro={errors.vigenciaInicio?.message} obrigatorio>
            <Input type="date" {...register("vigenciaInicio")} />
          </Field>
          <Field rotulo="Fim da vigência" erro={errors.vigenciaFim?.message} obrigatorio>
            <Input type="date" {...register("vigenciaFim")} />
          </Field>
        </div>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            Criar PEI
          </Button>
        </div>
      </form>
    </Modal>
  );
}

/** Aba PEI da ficha. A API já recorta por perfil (coordenador de curso só recebe versões publicadas do curso). */
export function PeisDoAluno({ alunoId }: { alunoId: string }) {
  const { usuario } = useSessao();
  const lista = useListaPeis(alunoId);
  const [criando, setCriando] = useState(false);
  const podeCriar = pode(usuario, "alunos.gerenciar");

  let corpo;
  if (lista.isPending)
    corpo = (
      <Carregando rotulo="Carregando PEIs">
        <Skeleton className="h-20 w-full rounded-bloco" />
      </Carregando>
    );
  else if (lista.isError) corpo = <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />;
  else if (lista.data.length === 0) corpo = <EmptyState icone={<NotebookPen />} titulo="Nenhum PEI" descricao="Os PEIs deste aluno aparecem aqui." />;
  else
    corpo = (
      // Substitui o skeleton: chega com o surgir padrão.
      <div className="flex animate-surgir flex-col gap-4">
        {lista.data.map((p) => (
          <div key={p.id} className="flex flex-col gap-2">
            <div className="text-denso text-suave">Responsável: {p.responsavel}</div>
            <ul className="m-0 flex list-none flex-col gap-2 p-0">
              {p.versoes.map((v) => (
                <li key={v.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-bloco border border-borda px-4 py-2.5 text-corpo">
                  <Link to={`/peis/versoes/${v.id}`} className="font-semibold text-primaria hover:underline">
                    Versão {v.numero}
                  </Link>
                  <Badge variante={VARIANTE_ESTADO[v.estado]}>{ROTULO_ESTADO[v.estado]}</Badge>
                  {v.foraDaVigencia && <Badge variante="aguardando">Vigência encerrada</Badge>}
                  <span className="text-suave">
                    Vigência {formatarDataCivil(v.vigenciaInicio)} a {formatarDataCivil(v.vigenciaFim)}
                  </span>
                  {v.publicadaEm && <span className="text-suave">Publicada em {formatarDataHora(v.publicadaEm)}</span>}
                  {v.cienciaRegistradaPorMim && <Badge variante="realizado">Ciência registrada</Badge>}
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    );

  return (
    <section aria-labelledby="titulo-peis" className={classeSecao}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="titulo-peis" className="m-0 font-titulo text-base font-bold">
          PEI
        </h2>
        {podeCriar && lista.data?.length === 0 && (
          <Button tamanho="sm" onClick={() => setCriando(true)}>
            Criar PEI
          </Button>
        )}
      </div>
      {corpo}
      {criando && <CriarPeiDialog alunoId={alunoId} aoFechar={() => setCriando(false)} />}
    </section>
  );
}
