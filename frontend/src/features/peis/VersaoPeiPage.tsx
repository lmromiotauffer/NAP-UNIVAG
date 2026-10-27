import { ChevronLeft } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { Badge } from "../../components/ui/Badge";
import { classeSecao, classeTituloPagina } from "../../components/ui/Card";
import { Carregando } from "../../components/ui/Carregando";
import { ErrorState } from "../../components/ui/ErrorState";
import { SecaoTexto as Texto } from "../../components/ui/Secao";
import { Skeleton } from "../../components/ui/Skeleton";
import { useToast } from "../../components/ui/Toast";
import { formatarDataCivil } from "../../lib/format";
import { AcoesVersao } from "./components/AcoesVersao";
import { AssinaturasEMCiencias } from "./components/AssinaturasEMCiencias";
import { ConteudoPeiForm } from "./components/ConteudoPeiForm";
import { useVersaoPei } from "./hooks/usePeis";
import { CAMPOS_PEDAGOGICOS, ROTULO_ESTADO, VARIANTE_ESTADO } from "./rotulos";

export function VersaoPeiPage() {
  const { id = "" } = useParams();
  const { usuario } = useSessao();
  const toast = useToast();
  const detalhe = useVersaoPei(id);
  // Remonta o formulário com os valores do servidor (depois de um 409).
  const [geracao, setGeracao] = useState(0);

  const voltar = (
    <Link
      to={detalhe.data && pode(usuario, "alunos.consultar") ? `/alunos/${detalhe.data.alunoId}` : "/"}
      aria-label="Voltar para a ficha do aluno"
      className="inline-flex items-center gap-0.5 self-start rounded-sm text-denso text-suave transition-colors duration-150 hover:text-texto pointer-coarse:min-h-11"
    >
      <ChevronLeft aria-hidden="true" className="-ml-1 size-4" />
      Ficha do aluno
    </Link>
  );

  const titulo = <title>{detalhe.data ? `PEI de ${detalhe.data.aluno} · NAP` : "PEI · NAP"}</title>;

  if (detalhe.isPending)
    return (
      <>
        {titulo}
        {voltar}
        <Carregando rotulo="Carregando PEI" className="flex flex-col gap-[18px]">
          <Skeleton className="h-8 w-72 max-w-full" />
          <Skeleton className="h-4 w-96 max-w-full" />
          <Skeleton className="h-40 max-w-3xl rounded-secao" />
        </Carregando>
      </>
    );
  if (detalhe.isError)
    return (
      <>
        {titulo}
        {voltar}
        <ErrorState erro={detalhe.error} aoTentarNovamente={() => void detalhe.refetch()} />
      </>
    );

  const v = detalhe.data;
  const c = v.conteudo;
  return (
    <>
      {titulo}
      {voltar}
      <header className="flex flex-col gap-3">
        <div className="min-w-0">
          <h1 className={classeTituloPagina}>PEI de {v.aluno}</h1>
          <div className="mt-1 flex flex-wrap items-center gap-x-1.5 gap-y-1 text-denso text-suave">
            <span>
              Matrícula {v.matricula} · {v.curso || "—"} · Responsável: {v.responsavel} · Versão {v.numero} · Vigência {formatarDataCivil(c.vigenciaInicio)} a {formatarDataCivil(c.vigenciaFim)}
            </span>
            <Badge variante={VARIANTE_ESTADO[v.estado]}>{ROTULO_ESTADO[v.estado]}</Badge>
            {v.foraDaVigencia && <Badge variante="aguardando">Vigência encerrada</Badge>}
          </div>
        </div>
        <AcoesVersao key={v.id} versao={v} />
      </header>

      <div className="flex max-w-3xl flex-col gap-3">
        {v.acoes.podeEditar ? (
          // O formulário parte do snapshot da abertura; remonta se a versão mudar de estado (ex.: devolvida).
          <section aria-labelledby="titulo-conteudo" className={classeSecao}>
            <h2 id="titulo-conteudo" className="m-0 font-titulo text-base font-bold">
              Conteúdo
            </h2>
            {/* Após "Recarregar valores atuais" o formulário remontado chega com o surgir: os campos trocaram de conteúdo. A primeira montagem já anima pelo <main>. */}
            <div key={`${v.id}-${v.estado}-${geracao}`} className={geracao > 0 ? "animate-surgir" : undefined}>
              <ConteudoPeiForm
                versao={v}
                aoEnviado={() => toast.sucesso("Versão enviada para revisão.")}
                aoConflito={() => void detalhe.refetch().then(() => setGeracao((g) => g + 1))}
              />
            </div>
          </section>
        ) : (
          <>
            {CAMPOS_PEDAGOGICOS.map(([campo, rotulo]) => (
              <Texto key={campo} titulo={rotulo}>
                {c[campo] || "Não preenchido."}
              </Texto>
            ))}
            {/* Nulo e sem permissão (coordenação de curso): a seção some. */}
            {(c.demandaInterna != null || pode(usuario, "alunos.gerenciar")) && (
              <Texto titulo="Demanda interna" nota="Uso interno do NAP — não faz parte do documento assinado.">
                {c.demandaInterna || "Não preenchida."}
              </Texto>
            )}
          </>
        )}
        <AssinaturasEMCiencias versao={v} />
      </div>
    </>
  );
}
