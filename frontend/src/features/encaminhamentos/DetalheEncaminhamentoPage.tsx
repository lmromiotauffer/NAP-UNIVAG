import { ChevronLeft } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Link, useParams } from "react-router";
import { pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { classeSecao, classeTituloPagina } from "../../components/ui/Card";
import { Carregando } from "../../components/ui/Carregando";
import { Dado as DadoBase } from "../../components/ui/Dado";
import { ErrorState } from "../../components/ui/ErrorState";
import { Modal } from "../../components/ui/Modal";
import { SecaoTexto as Texto } from "../../components/ui/Secao";
import { Skeleton } from "../../components/ui/Skeleton";
import { useToast } from "../../components/ui/Toast";
import { cn } from "../../lib/cn";
import { formatarDataCivil, formatarDataHora } from "../../lib/format";
import type { EncaminhamentoDetalhe } from "../../lib/types";
import { ComunicacoesVinculadas } from "../comunicacoes/components/ComunicacoesVinculadas";
import { EscreverMensagem } from "../comunicacoes/components/EscreverMensagem";
import { CancelarEncaminhamentoDialog } from "./components/CancelarEncaminhamentoDialog";
import { SeloEstado } from "./components/ListaEncaminhamentos";
import { RetornoDialog } from "./components/RetornoDialog";
import { useDetalheEncaminhamento } from "./hooks/useEncaminhamentos";
import {
  ACOES_POR_ESTADO,
  BOTAO_ACAO,
  especialidadeEDestino,
  notaMotivoRestrito,
  ROTULO_ACAO,
  ROTULO_ESTADO,
  VARIANTE_ESTADO,
  type AcaoRegistravel,
} from "./rotulos";

const Dado = (p: { rotulo: string; children: ReactNode }) => <DadoBase classeValor="text-corpo" {...p} />;

function Evolucoes({ e }: { e: EncaminhamentoDetalhe }) {
  // Só o retorno registrado depois da montagem chega animado: na abertura a seção já vem com o surgir do <main>.
  const [tamanhoInicial] = useState(e.evolucoes.length);
  return (
    <section aria-labelledby="titulo-evolucoes" className={classeSecao}>
      <h2 id="titulo-evolucoes" className="m-0 font-titulo text-base font-bold">
        Linha do tempo
      </h2>
      {e.evolucoes.length === 0 ? (
        <p className="m-0 text-denso text-suave">Nenhum retorno registrado ainda.</p>
      ) : (
        <ol className="m-0 flex list-none flex-col gap-3 border-l-2 border-borda p-0 pl-4">
          {e.evolucoes.map((ev, i) => (
            // Ordem cronológica com chave estável: depois de registrar um retorno só o item novo monta, e chega animado no fim da linha.
            <li key={`${ev.em}-${i}`} className={cn("flex flex-col gap-0.5 text-corpo", i >= tamanhoInicial && "animate-surgir")}>
              <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                <strong className="font-semibold">{ROTULO_ACAO[ev.acao]}</strong>
                <Badge variante={VARIANTE_ESTADO[ev.estadoNovo]}>{ROTULO_ESTADO[ev.estadoNovo]}</Badge>
                {ev.data != null && <span className="text-suave">em {formatarDataCivil(ev.data)}</span>}
              </div>
              <div className="text-rotulo text-suave">
                {ev.autor} · registrado {formatarDataHora(ev.em)}
              </div>
              {ev.texto != null && <p className="m-0 break-words whitespace-pre-line">{ev.texto}</p>}
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

export function DetalheEncaminhamentoPage() {
  const { id = "" } = useParams();
  const { usuario } = useSessao();
  const toast = useToast();
  const detalhe = useDetalheEncaminhamento(id);
  const [dialogo, setDialogo] = useState<AcaoRegistravel | "cancelar" | "mensagem" | null>(null);

  const titulo = <title>{detalhe.data ? `${detalhe.data.aluno} · Encaminhamentos · NAP` : "Encaminhamentos · NAP"}</title>;
  const voltar = (
    <Link
      to="/encaminhamentos"
      aria-label="Voltar para a lista de encaminhamentos"
      className="inline-flex items-center gap-0.5 self-start rounded-sm text-denso pointer-coarse:min-h-11 text-suave transition-colors duration-150 hover:text-texto"
    >
      <ChevronLeft aria-hidden="true" className="-ml-1 size-4" />
      Encaminhamentos
    </Link>
  );

  if (detalhe.isPending)
    return (
      <>
        {titulo}
        {voltar}
        <Carregando rotulo="Carregando encaminhamento" className="flex flex-col gap-[18px]">
          <Skeleton className="h-8 w-72 max-w-full" />
          <Skeleton className="h-4 w-96 max-w-full" />
          <Skeleton className="h-24 max-w-3xl rounded-secao" />
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

  const e = detalhe.data;
  const acoes = e.podeRegistrarRetorno ? ACOES_POR_ESTADO[e.estado] : [];
  const podeComunicar = pode(usuario, "comunicacoes");
  const fechar = () => setDialogo(null);
  const concluir = (mensagem: string) => () => {
    toast.sucesso(mensagem);
    setDialogo(null);
  };

  return (
    <>
      {titulo}
      {voltar}
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className={classeTituloPagina}>
            {/* O SIP não tem ficha do aluno: o nome só vira link para quem pode consultá-la. */}
            {pode(usuario, "alunos.consultar") ? (
              <Link to={`/alunos/${e.alunoId}`} className="rounded-sm hover:underline">
                {e.aluno}
              </Link>
            ) : (
              e.aluno
            )}
          </h1>
          <div className="mt-1 flex flex-wrap items-center gap-x-1.5 gap-y-1 text-denso text-suave">
            <span>
              Matrícula {e.matricula} · {e.curso || "—"} · {especialidadeEDestino(e)}
            </span>
            <SeloEstado {...e} />
          </div>
        </div>
        {(acoes.length > 0 || e.podeCancelar || podeComunicar) && (
          <div className="flex flex-wrap gap-2">
            {acoes.map((a, i) => (
              <Button key={a} variante={i === 0 ? "primaria" : "secundaria"} className="rounded-full" onClick={() => setDialogo(a)}>
                {BOTAO_ACAO[a]}
              </Button>
            ))}
            {podeComunicar && (
              <Button variante="secundaria" className="rounded-full" onClick={() => setDialogo("mensagem")}>
                Enviar mensagem
              </Button>
            )}
            {e.podeCancelar && (
              <Button variante="secundaria" className="rounded-full text-urgente" onClick={() => setDialogo("cancelar")}>
                Cancelar encaminhamento
              </Button>
            )}
          </div>
        )}
      </header>

      <div className="grid items-start gap-3 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="flex min-w-0 flex-col gap-3">
          {/* Card de texto que sobra sozinho na última linha ocupa a largura toda. */}
          <div className="grid gap-3 md:grid-cols-2 md:[&>:last-child:nth-child(even)]:col-span-2">
            <section aria-label="Datas" className={`${classeSecao} md:col-span-2`}>
              <dl className="m-0 grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
                <Dado rotulo="Encaminhado em">{formatarDataCivil(e.data)}</Dado>
                <Dado rotulo="Solicitante">{e.solicitante}</Dado>
                <Dado rotulo="Acolhimento">{formatarDataCivil(e.dataAcolhimento)}</Dado>
                <Dado rotulo="Início do atendimento">{formatarDataCivil(e.dataPrimeiroAtendimento)}</Dado>
                <Dado rotulo="Encerramento">{formatarDataCivil(e.dataEncerramento)}</Dado>
                <Dado rotulo="Última atualização">{formatarDataHora(e.atualizadoEm)}</Dado>
              </dl>
            </section>
            {/* Campos restritos chegam nulos para quem não pode vê-los: a seção some, sem placeholder. */}
            {e.resumoCompartilhavel != null && (
              <Texto titulo="Resumo compartilhável" nota="Lido pelo destino; a coordenação do curso só o vê quando é o destino.">
                {e.resumoCompartilhavel}
              </Texto>
            )}
            {e.motivoRestrito != null && (
              <Texto titulo="Motivo restrito" nota={notaMotivoRestrito(e.destino)}>
                {e.motivoRestrito}
              </Texto>
            )}
            {e.observacoes != null && (
              <Texto titulo="Observações" nota="Visíveis só à equipe NAP.">
                {e.observacoes}
              </Texto>
            )}
          </div>
          {podeComunicar && (
            <ComunicacoesVinculadas
              vinculo={{ encaminhamentoId: e.id }}
              descricaoVazia="As mensagens sobre este encaminhamento de que você participa aparecem aqui."
            />
          )}
        </div>
        <Evolucoes e={e} />
      </div>

      {dialogo === "mensagem" ? (
        <Modal aberto aoFechar={fechar} titulo="Enviar mensagem">
          <EscreverMensagem
            aluno={{ id: e.alunoId, nome: e.aluno, matricula: e.matricula }}
            encaminhamentoId={e.id}
            aoCancelar={fechar}
            aoConcluir={(acao) => concluir(acao === "enviada" ? "Mensagem enviada." : "Rascunho salvo.")()}
          />
        </Modal>
      ) : dialogo === "cancelar" ? (
        <CancelarEncaminhamentoDialog encaminhamento={e} aoFechar={fechar} aoConcluir={concluir("Encaminhamento cancelado.")} />
      ) : (
        dialogo && <RetornoDialog key={dialogo} encaminhamento={e} acao={dialogo} aoFechar={fechar} aoConcluir={concluir("Retorno registrado.")} />
      )}
    </>
  );
}
