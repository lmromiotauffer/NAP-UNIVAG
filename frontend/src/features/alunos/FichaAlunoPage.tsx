import { ChevronLeft } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { iniciais } from "../../components/layout/AppShell";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { classeSecao, classeTituloPagina } from "../../components/ui/Card";
import { Carregando } from "../../components/ui/Carregando";
import { Dado } from "../../components/ui/Dado";
import { ErrorState } from "../../components/ui/ErrorState";
import { ListaAbas } from "../../components/ui/ListaAbas";
import { Modal } from "../../components/ui/Modal";
import { Skeleton } from "../../components/ui/Skeleton";
import { classeMolduraRolavel } from "../../components/ui/Table";
import { useToast } from "../../components/ui/Toast";
import { cn } from "../../lib/cn";
import type { AlunoFicha } from "../../lib/types";
import { AtendimentoForm } from "../atendimentos/components/AtendimentoForm";
import { DetalheAtendimento } from "../atendimentos/components/DetalheAtendimento";
import { ListaAtendimentos } from "../atendimentos/components/ListaAtendimentos";
import { DocumentosDoAluno } from "../documentos/components/DocumentosDoAluno";
import { PeisDoAluno } from "../peis/components/PeisDoAluno";
import { ComunicacoesVinculadas } from "../comunicacoes/components/ComunicacoesVinculadas";
import { EscreverMensagem } from "../comunicacoes/components/EscreverMensagem";
import { EncaminhamentoForm } from "../encaminhamentos/components/EncaminhamentoForm";
import { ListaEncaminhamentos } from "../encaminhamentos/components/ListaEncaminhamentos";
import { AlterarSituacaoDialog } from "./components/AlterarSituacaoDialog";
import { ROTULO_SITUACAO, VARIANTE_SITUACAO } from "./rotulos";
import { AlunoForm } from "./components/AlunoForm";
import { LinhaDoTempo } from "./components/LinhaDoTempo";
import { ResumoAcompanhamento } from "./components/ResumoAcompanhamento";
import { TransferirCursoDialog } from "./components/TransferirCursoDialog";
import { useFichaAluno } from "./hooks/useAlunos";

type Aba = "resumo" | "atendimentos" | "encaminhamentos" | "comunicacoes" | "documentos" | "pei" | "linha";
type Dialogo = "editar" | "situacao" | "transferir" | "registrar" | "encaminhar" | "mensagem" | null;
// Listas da ficha mostram menos por página que as de página inteira: cabem na tela abaixo do cabeçalho e dos cartões.
const TAMANHO_LISTA = 10;

function ResumoPedagogico({ aluno }: { aluno: AlunoFicha }) {
  return (
    <section aria-labelledby="titulo-resumo" className={cn(classeSecao, "gap-3.5")}>
      <h2 id="titulo-resumo" className="m-0 font-titulo text-base font-bold">
        Resumo do acompanhamento
      </h2>
      {/* Os dois textos lado a lado no desktop: a seção ocupa a largura do painel em vez de deixar metade vazia. */}
      <dl className="grid gap-3.5 md:grid-cols-2 md:gap-6">
        <Dado rotulo="Necessidades educacionais" classeValor="text-corpo whitespace-pre-line">
          {aluno.necessidadesEducacionais || "—"}
        </Dado>
        <Dado rotulo="Observações acadêmicas" classeValor="text-corpo whitespace-pre-line">
          {aluno.observacoesAcademicas || "—"}
        </Dado>
      </dl>
      <p className="m-0 rounded-xl bg-realce px-3.5 py-3 text-rotulo text-suave">
        Queixas clínicas, diagnósticos e notas privadas ficam nos atendimentos e são visíveis apenas ao NAP.
      </p>
    </section>
  );
}

function AtendimentosDoAluno({ aluno, aoRegistrar }: { aluno: AlunoFicha; aoRegistrar: () => void }) {
  const [pagina, setPagina] = useState(1);
  const [aberto, setAberto] = useState<string | null>(null);
  const registrar = (
    <Button tamanho="sm" onClick={aoRegistrar}>
      Registrar atendimento
    </Button>
  );
  return (
    <section aria-labelledby="titulo-atendimentos" className={classeMolduraRolavel}>
      <div className="flex flex-wrap items-center justify-between gap-2 px-[22px] pt-4 pb-2">
        <h2 id="titulo-atendimentos" className="m-0 font-titulo text-base font-bold">
          Atendimentos
        </h2>
        {registrar}
      </div>
      <ListaAtendimentos
        filtro={{ alunoId: aluno.id, pagina, tamanho: TAMANHO_LISTA }}
        comFiltro={false}
        semAluno
        aoMudarPagina={setPagina}
        aoAbrir={setAberto}
        vazio={{ titulo: "Nenhum atendimento registrado", descricao: "Os atendimentos e ausências deste aluno aparecem aqui." }}
      />
      {aberto && <DetalheAtendimento key={aberto} id={aberto} aoFechar={() => setAberto(null)} />}
    </section>
  );
}

function EncaminhamentosDoAluno({ aluno, aoEncaminhar }: { aluno: AlunoFicha; aoEncaminhar?: () => void }) {
  const [pagina, setPagina] = useState(1);
  const encaminhar = aoEncaminhar && (
    <Button tamanho="sm" onClick={aoEncaminhar}>
      Novo encaminhamento
    </Button>
  );
  return (
    <section aria-labelledby="titulo-encaminhamentos" className={classeMolduraRolavel}>
      <div className="flex flex-wrap items-center justify-between gap-2 px-[22px] pt-4 pb-2">
        <h2 id="titulo-encaminhamentos" className="m-0 font-titulo text-base font-bold">
          Encaminhamentos
        </h2>
        {encaminhar}
      </div>
      <ListaEncaminhamentos
        filtro={{ alunoId: aluno.id, pagina, tamanho: TAMANHO_LISTA }}
        comFiltro={false}
        semAluno
        aoMudarPagina={setPagina}
        vazio={{ titulo: "Nenhum encaminhamento", descricao: "Os encaminhamentos deste aluno aparecem aqui." }}
      />
    </section>
  );
}

function FichaCarregando() {
  return (
    <Carregando rotulo="Carregando ficha" className="flex flex-col gap-[18px]">
      <div className="flex items-center gap-4">
        <Skeleton className="size-14 flex-none rounded-full" />
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          <Skeleton className="h-7 w-64 max-w-full" />
          <Skeleton className="h-4 w-80 max-w-full" />
        </div>
      </div>
      <div className="grid grid-cols-[repeat(auto-fit,minmax(150px,1fr))] gap-3">
        {Array.from({ length: 4 }, (_, i) => (
          <Skeleton key={i} className="h-[104px] rounded-cartao" />
        ))}
      </div>
      <Skeleton className="h-10 w-72" />
      <Skeleton className="h-56 max-w-3xl rounded-secao" />
    </Carregando>
  );
}

export function FichaAlunoPage() {
  const { id = "" } = useParams();
  const { usuario } = useSessao();
  const navigate = useNavigate();
  const toast = useToast();
  const ficha = useFichaAluno(id);
  const [aba, setAba] = useState<Aba>("resumo");
  const [dialogo, setDialogo] = useState<Dialogo>(null);

  const podeGerenciar = pode(usuario, "alunos.gerenciar");
  // A aba, o botão e o formulário de registro dependem da mesma capacidade.
  const podeRegistrar = pode(usuario, "atendimentos.registrar");
  const podeEncaminhar = pode(usuario, "encaminhamentos.criar");
  const podeComunicar = pode(usuario, "comunicacoes");
  // Coordenação de curso: Resumo, Encaminhamentos (do próprio curso, sem motivo restrito), Documentos (só os compartilhados), PEI (só versões publicadas do curso) e Linha do tempo.
  const abas: { chave: Aba; rotulo: string }[] = [
    { chave: "resumo", rotulo: "Resumo" },
    ...(podeRegistrar ? [{ chave: "atendimentos" as const, rotulo: "Atendimentos" }] : []),
    ...(pode(usuario, "encaminhamentos") ? [{ chave: "encaminhamentos" as const, rotulo: "Encaminhamentos" }] : []),
    ...(podeComunicar ? [{ chave: "comunicacoes" as const, rotulo: "Comunicações" }] : []),
    ...(pode(usuario, "alunos.consultar") ? [{ chave: "documentos" as const, rotulo: "Documentos" }] : []),
    ...(pode(usuario, "peis") ? [{ chave: "pei" as const, rotulo: "PEI" }] : []),
    { chave: "linha", rotulo: "Linha do tempo" },
  ];

  const voltar = (
    <Link
      to="/alunos"
      aria-label="Voltar para a lista de alunos"
      className="inline-flex items-center gap-0.5 self-start rounded-sm text-denso text-suave transition-colors duration-150 hover:text-texto pointer-coarse:min-h-11"
    >
      <ChevronLeft aria-hidden="true" className="-ml-1 size-4" />
      Alunos
    </Link>
  );

  const titulo = <title>{ficha.data ? `${ficha.data.nome} · Alunos · NAP` : "Alunos · NAP"}</title>;

  if (ficha.isPending)
    return (
      <>
        {titulo}
        {voltar}
        <FichaCarregando />
      </>
    );
  if (ficha.isError)
    return (
      <>
        {titulo}
        {voltar}
        <ErrorState erro={ficha.error} aoTentarNovamente={() => void ficha.refetch()} />
      </>
    );

  const aluno = ficha.data;

  return (
    <>
      {titulo}
      {voltar}
      <header className="flex flex-wrap items-start justify-between gap-4">
        {/* basis-48 em rem: com texto grande o nome desce para baixo do avatar em vez de estourar a largura. */}
        <div className="flex min-w-0 flex-wrap items-center gap-4">
          <div aria-hidden="true" className="grid size-14 flex-none place-items-center rounded-full bg-bloco font-titulo text-lg font-black text-bloco-texto">
            {iniciais(aluno.nome)}
          </div>
          <div className="min-w-0 flex-1 basis-48">
            <h1 className={classeTituloPagina}>{aluno.nome}</h1>
            <div className="mt-1 flex flex-wrap items-center gap-x-1.5 text-denso text-suave">
              <span className="min-w-0 break-words">
                Matrícula {aluno.matricula}
                {aluno.matriculaAtiva != null && ` (${aluno.matriculaAtiva ? "ativa" : "inativa"})`} · {aluno.curso || "—"}
                {aluno.semestre != null && ` · ${aluno.semestre}º semestre`}
              </span>
              {aluno.situacao && <Badge variante={VARIANTE_SITUACAO[aluno.situacao]}>{ROTULO_SITUACAO[aluno.situacao]}</Badge>}
            </div>
          </div>
        </div>
        {podeGerenciar && (
          <div className="flex flex-wrap gap-2">
            {/* Única ação primária: agendar é o próximo passo do acompanhamento; editar e alterar ficam secundárias. */}
            {pode(usuario, "agenda") && (
              <Button className="rounded-full" onClick={() => void navigate(`/agenda?aluno=${aluno.id}`)}>
                Agendar
              </Button>
            )}
            <Button variante="secundaria" className="rounded-full" onClick={() => setDialogo("editar")}>
              Editar dados
            </Button>
            <Button variante="secundaria" className="rounded-full" onClick={() => setDialogo("situacao")}>
              Alterar situação
            </Button>
            <Button variante="secundaria" className="rounded-full" onClick={() => setDialogo("transferir")}>
              Transferir curso
            </Button>
          </div>
        )}
      </header>

      <ResumoAcompanhamento aluno={aluno} />

      {/* Abas e painel num bloco só que ocupa o resto da tela no desktop: o painel absorve o encolhimento (as listas rolam por dentro, com a paginação à vista) e o tablist mantém a altura. */}
      <div className="flex flex-col gap-[18px] md:min-h-0 md:flex-1">
        <ListaAbas abas={abas} atual={aba} aoTrocar={setAba} rotulo="Seções da ficha" />
        {/* `key` remonta o painel ao trocar de aba (chegada com surgir); o resumo é só texto, então leva tabIndex (padrão WAI-ARIA). */}
        <div key={aba} role="tabpanel" id={`painel-${aba}`} aria-labelledby={`aba-${aba}`} tabIndex={aba === "resumo" ? 0 : undefined} className="flex animate-surgir flex-col md:min-h-0 md:flex-1">
          {aba === "resumo" && <ResumoPedagogico aluno={aluno} />}
          {aba === "atendimentos" && podeRegistrar && <AtendimentosDoAluno aluno={aluno} aoRegistrar={() => setDialogo("registrar")} />}
          {aba === "encaminhamentos" && <EncaminhamentosDoAluno aluno={aluno} aoEncaminhar={podeEncaminhar ? () => setDialogo("encaminhar") : undefined} />}
          {aba === "comunicacoes" && podeComunicar && (
            <ComunicacoesVinculadas
              vinculo={{ alunoId: aluno.id }}
              descricaoVazia="As mensagens sobre este aluno de que você participa aparecem aqui."
              aoEscrever={() => setDialogo("mensagem")}
            />
          )}
          {aba === "documentos" && pode(usuario, "alunos.consultar") && <DocumentosDoAluno alunoId={aluno.id} />}
          {aba === "pei" && pode(usuario, "peis") && <PeisDoAluno alunoId={aluno.id} />}
          {aba === "linha" && <LinhaDoTempo alunoId={aluno.id} />}
        </div>
      </div>

      {podeGerenciar && (
        <>
          <Modal aberto={dialogo === "editar"} aoFechar={() => setDialogo(null)} titulo="Editar dados do aluno">
            <AlunoForm
              aluno={aluno}
              aoCancelar={() => setDialogo(null)}
              aoConcluir={() => {
                toast.sucesso("Dados do aluno atualizados.");
                setDialogo(null);
              }}
            />
          </Modal>
          {/* Montados só abertos: cada abertura congela a versão da ficha naquele momento. */}
          {dialogo === "situacao" && <AlterarSituacaoDialog aluno={aluno} aberto aoFechar={() => setDialogo(null)} />}
          {dialogo === "transferir" && <TransferirCursoDialog aluno={aluno} aberto aoFechar={() => setDialogo(null)} />}
        </>
      )}
      {podeEncaminhar && (
        <Modal aberto={dialogo === "encaminhar"} aoFechar={() => setDialogo(null)} titulo="Novo encaminhamento">
          <EncaminhamentoForm
            aluno={{ id: aluno.id, nome: aluno.nome, matricula: aluno.matricula }}
            aoCancelar={() => setDialogo(null)}
            aoConcluir={() => {
              toast.sucesso("Encaminhamento registrado.");
              setDialogo(null);
            }}
          />
        </Modal>
      )}
      {podeComunicar && (
        <Modal aberto={dialogo === "mensagem"} aoFechar={() => setDialogo(null)} titulo="Nova mensagem">
          {dialogo === "mensagem" && (
            <EscreverMensagem
              aluno={{ id: aluno.id, nome: aluno.nome, matricula: aluno.matricula }}
              aoCancelar={() => setDialogo(null)}
              aoConcluir={(acao) => {
                toast.sucesso(acao === "enviada" ? "Mensagem enviada." : "Rascunho salvo.");
                setDialogo(null);
              }}
            />
          )}
        </Modal>
      )}
      {podeRegistrar && (
        <Modal aberto={dialogo === "registrar"} aoFechar={() => setDialogo(null)} titulo="Registrar atendimento avulso">
          <AtendimentoForm
            aluno={{ id: aluno.id, nome: aluno.nome, matricula: aluno.matricula }}
            aoCancelar={() => setDialogo(null)}
            aoConcluir={() => {
              toast.sucesso("Atendimento registrado.");
              setDialogo(null);
            }}
          />
        </Modal>
      )}
    </>
  );
}
