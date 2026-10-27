import { CalendarClock, ChevronLeft, ChevronRight, LayoutGrid, Mail, Maximize2, UserX } from "lucide-react";
import { useId, useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { ehCoordenacao, pode, rotuloPerfil } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { GraficoAnel, type FatiaAnel } from "../../components/graficos/GraficoAnel";
import { GraficoBarras, type DadoGrafico } from "../../components/graficos/GraficoBarras";
import { CORES_AJUDA } from "../../components/layout/AppShell";
import { Button } from "../../components/ui/Button";
import { Card, classeTituloPagina } from "../../components/ui/Card";
import { CartaoSecao as Secao, classeDetalhes } from "../../components/ui/Secao";
import { Carregando } from "../../components/ui/Carregando";
import { EmptyState } from "../../components/ui/EmptyState";
import { ErrorState } from "../../components/ui/ErrorState";
import { Modal } from "../../components/ui/Modal";
import { Select } from "../../components/ui/Select";
import { Skeleton } from "../../components/ui/Skeleton";
import type { VarianteSemantica } from "../../components/ui/variantes";
import { cn } from "../../lib/cn";
import {
  diaValido,
  formatarDataLonga,
  formatarDuracao,
  formatarHora,
  formatarInteiro,
  formatarMesCurto,
  formatarPercentual,
  formatarSemana,
  hojeCivil,
  saudacao,
  segundaDoDia,
  somarDias,
} from "../../lib/format";
import type { Contagem, Painel, ProducaoSemana, SerieMensal } from "../../lib/types";
import { VARIANTE_SITUACAO, type Situacao } from "../alunos/rotulos";
import { useContagemMensagens } from "../comunicacoes/hooks/useComunicacoes";
import { useCursos, useProfissionais } from "../compartilhado/catalogos";
import { AgendaHoje } from "./components/AgendaHoje";
import { BlocoAtividades } from "./components/BlocoAtividades";
import { BlocoEncaminhamentos } from "./components/BlocoEncaminhamentos";
import { BlocoPendencias } from "./components/BlocoPendencias";
import { BlocoPei } from "./components/BlocoPei";
import { CartaoIndicador } from "./components/CartaoIndicador";
import { TabelaPorProfissional } from "./components/TabelaPorProfissional";
import { usePainel, type FiltroPainel } from "./hooks/usePainel";

type Detalhe = "semana" | "taxa" | "meses" | "cursos" | null;

// Traço do anel e marcador da legenda pelas classes de token da variante semântica.
const COR_VARIANTE: Record<VarianteSemantica, [string, string]> = {
  info: ["stroke-info", "bg-info"],
  realizado: ["stroke-realizado", "bg-realizado"],
  neutro: ["stroke-suave", "bg-suave"],
  urgente: ["stroke-urgente", "bg-urgente"],
  aguardando: ["stroke-aguardando", "bg-aguardando"],
  atividade: ["stroke-atividade", "bg-atividade"],
};

/** "Sua" só quando a agenda exibida é a do próprio usuário (sem filtro, a API força esse escopo para quem não é coordenação). */
function tituloAgendaHoje(coordenacao: boolean, filtroProfissionalId: string | undefined, usuarioId: string | undefined, nome: string | undefined): string {
  if (!coordenacao || (filtroProfissionalId && filtroProfissionalId === usuarioId)) return "Sua agenda hoje";
  if (!filtroProfissionalId) return "Agenda de hoje · equipe";
  return `Agenda de hoje · ${nome ?? "profissional"}`;
}

const juntarNotas = (...notas: (string | undefined)[]) => notas.filter(Boolean).join(" · ") || undefined;

/** A API já entrega os 6 meses em ordem (lacunas zeradas, só o corrente parcial, nunca futuro): só rotula. */
export function serieSeisMeses(serie: SerieMensal[]): DadoGrafico[] {
  return serie.map((s) => ({ rotulo: formatarMesCurto(s.mes), valor: s.realizados, parcial: s.parcial }));
}

function fatiasSituacao(contagens: Contagem[]): FatiaAnel[] {
  return contagens.map((c) => {
    const [classe, classeLegenda] = COR_VARIANTE[VARIANTE_SITUACAO[c.chave as Situacao] ?? "neutro"];
    return { rotulo: c.rotulo, valor: c.total, classe, classeLegenda };
  });
}

function montarFatias(itens: [string, number, VarianteSemantica][]): FatiaAnel[] {
  return itens.map(([rotulo, valor, variante]) => {
    const [classe, classeLegenda] = COR_VARIANTE[variante];
    return { rotulo, valor, classe, classeLegenda };
  });
}

/** Fatias do anel da semana: realizados e faltas vêm dos atendimentos registrados; sem desfecho são compromissos passados sem registro. Disjuntos. */
function fatiasSemana(s: ProducaoSemana): FatiaAnel[] {
  return montarFatias([
    ["Realizados", s.atendimentosRealizados, "realizado"],
    ["Faltas", s.faltas, "urgente"],
    ["Sem desfecho", s.compromissosSemDesfecho, "neutro"],
  ]);
}

/** Base da taxa de ausência: só atendimentos individuais, como no backend (reuniões ficam fora). */
function fatiasAusencia(s: ProducaoSemana): FatiaAnel[] {
  return montarFatias([
    ["Individuais realizados", s.individuais, "realizado"],
    ["Faltas", s.faltas, "urgente"],
  ]);
}

function Legenda() {
  return (
    // No celular a legenda ocuparia meia tela: fica só na Ajuda, que traz as mesmas cores.
    // Visível só o nome curto (até os dois-pontos); o texto completo vai no title e para leitores de tela.
    <ul aria-label="Significado das cores" className="m-0 -mt-1 hidden list-none flex-wrap gap-x-4 gap-y-1 p-0 text-xs text-suave sm:flex">
      {CORES_AJUDA.map(([cor, , descricao]) => {
        const [curto, resto] = descricao.replace(/\.$/, "").split(": ");
        return (
          <li key={cor} title={descricao} className="flex items-center gap-1.5">
            <span aria-hidden="true" className={cn("size-[9px] rounded-[3px]", cor)} />
            {curto}
            {resto && <span className="sr-only">: {resto}</span>}
          </li>
        );
      })}
    </ul>
  );
}

function CartaoRealizados({ semana, parcial, aoDetalhar }: { semana: ProducaoSemana; parcial: boolean; aoDetalhar: () => void }) {
  const reunioes = semana.atendimentosRealizados - semana.individuais;
  return (
    // No celular o conteúdo desce abaixo do botão Detalhes e usa a largura toda; a partir do sm o botão ganha uma coluna à direita.
    <div className="relative flex h-full min-w-0 items-center gap-4 rounded-cartao bg-bloco px-[18px] pt-12 pb-3.5 text-bloco-texto sm:py-3.5 sm:pr-24 pointer-coarse:pt-16 pointer-coarse:sm:py-3.5">
      <button
        type="button"
        onClick={aoDetalhar}
        aria-label="Detalhes: atendimentos da semana"
        className={cn(classeDetalhes, "absolute top-3 right-3 border-bloco-texto/20 text-bloco-texto hover:bg-bloco-texto/10 focus-visible:outline-bloco-texto")}
      >
        <Maximize2 aria-hidden="true" className="size-3" />
        Detalhes
      </button>
      <div className="flex-none font-titulo text-[3.375rem] leading-[0.9] font-black tracking-[-0.04em] tabular-nums">{formatarInteiro(semana.atendimentosRealizados)}</div>
      {/* Uma informação por linha: o complemento nunca quebra no meio ("semana até / agora"). */}
      <div className="flex min-w-0 flex-col gap-0.5">
        <strong className="text-rotulo font-semibold">Atendimentos realizados</strong>
        <div className="text-rotulo text-bloco-texto/70">
          {formatarDuracao(semana.minutosRealizados)} · <span className="whitespace-nowrap">{parcial ? "semana até agora" : "semana"}</span>
        </div>
        <div className="text-xs text-bloco-texto/70">
          {formatarInteiro(semana.individuais)} individuais + {formatarInteiro(reunioes)} {reunioes === 1 ? "reunião" : "reuniões"} · ausências não contam
        </div>
      </div>
    </div>
  );
}

function PainelCarregando() {
  return (
    <Carregando rotulo="Carregando painel" className="flex flex-col gap-3">
      <div className="grid grid-cols-[minmax(0,1fr)] gap-3 sm:grid-cols-2 xl:grid-cols-[2fr_1fr_1fr]">
        <Skeleton className="h-[92px] rounded-cartao" />
        <Skeleton className="h-[92px] rounded-cartao" />
        <Skeleton className="h-[92px] rounded-cartao" />
      </div>
      <div className="grid grid-cols-[minmax(0,1fr)] gap-3 md:grid-cols-2 xl:grid-cols-3">
        <Skeleton className="h-64 rounded-cartao" />
        <Skeleton className="h-64 rounded-cartao" />
        <Skeleton className="h-64 rounded-cartao" />
      </div>
    </Carregando>
  );
}

function Filtros({ segunda, hoje }: { segunda: string; hoje: string }) {
  const [params, setParams] = useSearchParams();
  const profissionais = useProfissionais();
  const cursos = useCursos();
  const id = useId();
  const atual = segundaDoDia(hoje);

  function alterar(chave: keyof FiltroPainel, valor: string) {
    setParams(
      (p) => {
        const proximo = new URLSearchParams(p);
        if (valor && !(chave === "semana" && valor === atual)) proximo.set(chave, valor);
        else proximo.delete(chave);
        return proximo;
      },
      { replace: true },
    );
  }

  // No celular os filtros crescem a partir do próprio conteúdo (sem truncar) e a semana ocupa a linha de baixo.
  // min-w: com o select customizável (index.css) a pílula fechada tem a largura do valor escolhido, não da opção mais longa; o piso evita a barra pular.
  const classeFiltro = "h-9 w-auto min-w-[9.5rem] flex-auto rounded-full py-0 text-rotulo sm:flex-none";
  return (
    <div role="search" aria-label="Filtrar painel" className="flex flex-wrap items-center gap-1.5 max-sm:w-full">
      <label htmlFor={`${id}-prof`} className="sr-only">
        Profissional
      </label>
      <Select id={`${id}-prof`} value={params.get("profissionalId") ?? ""} onChange={(e) => alterar("profissionalId", e.target.value)} className={classeFiltro}>
        <option value="">Profissional: toda a equipe</option>
        {profissionais.data?.map((p) => (
          <option key={p.id} value={p.id}>
            {p.nome}
          </option>
        ))}
      </Select>
      <label htmlFor={`${id}-curso`} className="sr-only">
        Curso
      </label>
      <Select id={`${id}-curso`} value={params.get("cursoId") ?? ""} onChange={(e) => alterar("cursoId", e.target.value)} className={classeFiltro}>
        <option value="">Curso: todos</option>
        {cursos.data?.map((c) => (
          <option key={c.id} value={c.id}>
            {c.nome}
          </option>
        ))}
      </Select>
      <div className="flex items-center justify-between gap-1 rounded-full bg-bloco px-1 max-sm:w-full text-rotulo text-bloco-texto">
        <Button tamanho="icone" variante="fantasma" aria-label="Semana anterior" className="size-8 rounded-full text-bloco-texto hover:bg-bloco-texto/10 focus-visible:outline-bloco-texto" onClick={() => alterar("semana", somarDias(segunda, -7))}>
          <ChevronLeft aria-hidden="true" className="size-4" />
        </Button>
        <span aria-live="polite" className="px-1 text-center sm:whitespace-nowrap">
          Semana {formatarSemana(segunda, 6)}
        </span>
        <Button
          tamanho="icone"
          variante="fantasma"
          aria-label="Próxima semana"
          disabled={segunda >= atual}
          className="size-8 rounded-full text-bloco-texto hover:bg-bloco-texto/10 focus-visible:outline-bloco-texto"
          onClick={() => alterar("semana", somarDias(segunda, 7))}
        >
          <ChevronRight aria-hidden="true" className="size-4" />
        </Button>
      </div>
    </div>
  );
}

export function PainelPage() {
  const { usuario } = useSessao();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [detalhe, setDetalhe] = useState<Detalhe>(null);
  const agora = new Date();
  const hoje = hojeCivil(agora);
  const coordenacao = ehCoordenacao(usuario);

  // Só coordenação/administração filtra; para os demais a API força o escopo e a tela não oferece filtros.
  const semanaUrl = params.get("semana") ?? "";
  const segunda = segundaDoDia(coordenacao && diaValido(semanaUrl) ? semanaUrl : hoje);
  const filtro: FiltroPainel = coordenacao
    ? { semana: segunda, profissionalId: params.get("profissionalId") || undefined, cursoId: params.get("cursoId") || undefined }
    : {};
  const painel = usePainel(filtro);
  // Fora do cache do painel: é a mesma consulta (e o mesmo ritmo) do selo da navegação.
  const podeComunicar = pode(usuario, "comunicacoes");
  const mensagens = useContagemMensagens(podeComunicar);
  const profissionais = useProfissionais(coordenacao);
  // Estes blocos não respeitam o filtro de profissional (a API conta a equipe toda): o título avisa.
  const notaEquipe = filtro.profissionalId ? "toda a equipe" : undefined;
  const primeiroNome = usuario?.nome.split(" ")[0] ?? "";

  const cabecalho = (
    <header className="flex flex-wrap items-center justify-between gap-3">
      <title>Painel · NAP</title>
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <h1 className={classeTituloPagina}>
          {saudacao(agora)}, {primeiroNome}.
        </h1>
        <span className="text-rotulo text-suave">
          {rotuloPerfil(usuario?.perfil ?? "")} · {formatarDataLonga(agora)} · {formatarHora(agora.toISOString())}
        </span>
      </div>
      {coordenacao && <Filtros segunda={segunda} hoje={hoje} />}
    </header>
  );

  if (painel.isPending)
    return (
      <>
        {cabecalho}
        <PainelCarregando />
      </>
    );
  if (painel.isError && !painel.data)
    return (
      <>
        {cabecalho}
        <ErrorState erro={painel.error} aoTentarNovamente={() => void painel.refetch()} />
      </>
    );

  const p: Painel = painel.data;
  const semana = p.semana;
  const meses = p.ultimos6Meses ? serieSeisMeses(p.ultimos6Meses) : null;
  const mesParcial = meses?.find((m) => m.parcial);
  const cursos = p.alunosPorCurso ? [...p.alunosPorCurso].sort((a, b) => b.total - a.total).map((c): DadoGrafico => ({ rotulo: c.rotulo, valor: c.total })) : null;
  const totalCursos = p.alunosPorCurso?.reduce((s, c) => s + c.total, 0) ?? 0;
  const totalSituacao = p.alunosPorSituacao?.reduce((s, c) => s + c.total, 0) ?? 0;
  const semBlocos = !semana && !meses && !cursos && !p.alunosPorSituacao && !p.alertas && !p.agendaHoje && !p.porProfissional && !p.encaminhamentos && !p.peis && !p.atividades;
  const tituloMeses = "Atendimentos realizados · 6 meses";
  // A linha média tem de 1 a 3 blocos conforme o perfil: as colunas acompanham para não sobrar buraco.
  const blocosMeio = [p.alertas, meses, cursos || p.alunosPorSituacao].filter(Boolean).length;
  // Com os 3 blocos e a produção, a partir do 2xl ela entra como 4ª coluna e "Pendente" (o mais alto) ocupa duas linhas,
  // com a agenda de hoje ao lado dele, sob os outros três: assim o painel cabe na tela sem rolar.
  // "Pendente" leva mais largura para o selo, o título e o link de cada alerta ficarem numa linha só.
  const quatroColunas = blocosMeio === 3 && !!p.porProfissional;
  const pendenteDuasLinhas = quatroColunas && !!p.agendaHoje;

  return (
    <>
      {cabecalho}
      <Legenda />
      {/* delay-100 só no esmaecer: troca de semana em menos de 100 ms não pisca; a volta ao normal é imediata. */}
      <div aria-busy={painel.isPlaceholderData} className={cn("flex flex-col gap-3 transition-opacity duration-150", painel.isPlaceholderData && "opacity-60 delay-100")}>
        {/* Aqui o erro (refetch em segundo plano) é inserido sobre o painel já visível, não substitui skeleton: chega como os demais avisos. */}
        {painel.isError && <ErrorState erro={painel.error} aoTentarNovamente={() => void painel.refetch()} className="animate-surgir" />}
        {semBlocos && !podeComunicar && (
          <Card>
            <EmptyState icone={<LayoutGrid />} titulo="Nada para mostrar" descricao="Não há indicadores disponíveis para o seu perfil." />
          </Card>
        )}
        {(semana || podeComunicar) && (
          // Grade única: o bloco escuro (primário) ocupa a linha toda até o xl; com Mensagens são 3 cartões e o ímpar ocupa a linha abaixo do lg.
          <div className={cn("grid grid-cols-2 gap-2 sm:gap-3", semana && podeComunicar ? "lg:grid-cols-3 xl:grid-cols-[2fr_1fr_1fr_1fr]" : "xl:grid-cols-[2fr_1fr_1fr]")}>
            {semana && (
              <>
                <div className="col-span-full min-w-0 xl:col-span-1">
                  <CartaoRealizados semana={semana} parcial={p.periodo.parcial} aoDetalhar={() => setDetalhe("semana")} />
                </div>
                <CartaoIndicador
                  titulo="Taxa de ausência"
                  valor={semana.taxaAusencia}
                  formatar={formatarPercentual}
                  base={`${formatarInteiro(semana.faltas)} ${semana.faltas === 1 ? "falta" : "faltas"} ÷ (${formatarInteiro(semana.individuais)} + ${formatarInteiro(semana.faltas)})`}
                  motivoNaoCalculado={semana.motivoNaoCalculado}
                  Icone={UserX}
                  classeIcone="text-urgente"
                  aoVerDetalhes={() => setDetalhe("taxa")}
                />
                <CartaoIndicador
                  titulo="Sem desfecho"
                  valor={semana.compromissosSemDesfecho}
                  formatar={formatarInteiro}
                  base="compromissos da semana sem registro"
                  Icone={CalendarClock}
                  classeIcone="text-suave"
                  aoVerDetalhes={pode(usuario, "agenda") ? () => void navigate("/agenda") : undefined}
                />
              </>
            )}
            {podeComunicar && (
              <CartaoIndicador
                titulo="Mensagens não lidas"
                valor={mensagens.data?.naoLidas ?? null}
                formatar={formatarInteiro}
                base="recebidas na sua caixa de entrada"
                motivoNaoCalculado={mensagens.isError ? "Não foi possível carregar." : mensagens.isPending ? "Carregando…" : null}
                Icone={Mail}
                classeIcone="text-info"
                aoVerDetalhes={() => void navigate("/comunicacoes")}
                className={semana ? "max-lg:col-span-2" : "col-span-2 sm:col-span-1"}
              />
            )}
          </div>
        )}
        {p.encaminhamentos && <BlocoEncaminhamentos dados={p.encaminhamentos} ehSip={usuario?.perfil === "Sip"} podeAbrir={pode(usuario, "encaminhamentos")} />}
        {/* Em telas largas PEI (3 cartões) e Atividades (2) dividem a linha, com cartões de largura parecida. */}
        {(p.peis || p.atividades) && (
          <div className={cn("grid gap-3", p.peis && p.atividades && "xl:grid-cols-[3fr_2fr]")}>
            {p.peis && <BlocoPei dados={p.peis} />}
            {p.atividades && <BlocoAtividades dados={p.atividades} podeAbrir={pode(usuario, "atividades")} />}
          </div>
        )}
        <div className={cn("grid gap-3", blocosMeio > 1 && "md:grid-cols-2", blocosMeio > 2 && "xl:grid-cols-3", quatroColunas && "2xl:grid-cols-[1.3fr_1fr_1fr_1fr]")}>
          {p.alertas && <BlocoPendencias alertas={p.alertas} className={cn(pendenteDuasLinhas && "2xl:row-span-2")} />}
          {meses && (
            <Secao titulo={tituloMeses} nota={mesParcial ? `${mesParcial.rotulo} parcial` : undefined} aoDetalhar={() => setDetalhe("meses")} rotuloDetalhar="Detalhes: valores dos 6 meses">
              <GraficoBarras
                titulo={tituloMeses}
                descricao={`Atendimentos realizados por mês, de ${meses[0]?.rotulo} a ${meses.at(-1)?.rotulo}. Ausências não contam.${mesParcial ? ` ${mesParcial.rotulo} é parcial: conta só até hoje.` : ""}`}
                dados={meses}
                unidade="Realizados"
                rotuloItem="Mês"
                orientacao="vertical"
                className="mx-auto mt-1 w-full max-w-[380px]"
              />
            </Secao>
          )}
          {(cursos || p.alunosPorSituacao) && (
            <Secao
              titulo={cursos ? "Alunos por curso" : "Alunos por situação"}
              nota={juntarNotas(cursos ? `${formatarInteiro(totalCursos)} alunos` : undefined, notaEquipe)}
              aoDetalhar={cursos ? () => setDetalhe("cursos") : undefined}
              rotuloDetalhar="Detalhes: tabela por curso"
            >
              {cursos &&
                (cursos.length === 0 ? (
                  <p className="m-0 py-4 text-sm text-suave">Nenhum aluno cadastrado.</p>
                ) : (
                  <GraficoBarras titulo="Alunos por curso" descricao="Alunos acompanhados por curso atual." dados={cursos} unidade="Alunos" rotuloItem="Curso" orientacao="horizontal" className="mx-auto w-full max-w-[320px]" />
                ))}
              {p.alunosPorSituacao && (
                <div className={cn("flex flex-wrap items-center gap-x-4 gap-y-1.5", cursos && "border-t border-borda pt-2.5")}>
                  <p className="m-0 text-xs text-suave">{juntarNotas("Situação", notaEquipe)}</p>
                  {totalSituacao === 0 ? (
                    <p className="m-0 text-sm text-suave">Nenhum aluno cadastrado.</p>
                  ) : (
                    <GraficoAnel titulo="Alunos por situação" descricao="Distribuição dos alunos por situação no NAP." dados={fatiasSituacao(p.alunosPorSituacao)} unidade="alunos" rotuloItem="Situação" />
                  )}
                </div>
              )}
            </Secao>
          )}
          {p.agendaHoje && (
            <AgendaHoje
              titulo={tituloAgendaHoje(coordenacao, filtro.profissionalId, usuario?.id, profissionais.data?.find((x) => x.id === filtro.profissionalId)?.nome)}
              compromissos={p.agendaHoje}
              mostrarProfissional={coordenacao && !filtro.profissionalId}
              agora={agora}
              className={cn("col-span-full", quatroColunas && "2xl:order-last", pendenteDuasLinhas && "2xl:col-span-3")}
            />
          )}
          {p.porProfissional && (
            <Secao titulo="Produção por profissional · semana" nota={juntarNotas(formatarSemana(p.periodo.inicio, 6), notaEquipe)} className={cn("col-span-full", quatroColunas && "2xl:col-span-1")}>
              <TabelaPorProfissional dados={p.porProfissional} />
            </Secao>
          )}
        </div>
      </div>

      <Modal aberto={detalhe === "semana"} aoFechar={() => setDetalhe(null)} titulo={`Atendimentos · semana ${formatarSemana(p.periodo.inicio, 6)}`}>
        {semana && (
          <div className="flex flex-col gap-4 text-sm">
            {p.periodo.parcial && <p className="m-0 text-suave">Semana em andamento: os números contam só até agora.</p>}
            <GraficoAnel
              titulo="Compromissos da semana por desfecho"
              descricao="Compromissos da semana já passados, divididos em realizados, faltas e sem desfecho. Remarcados e cancelados ficam de fora."
              dados={fatiasSemana(semana)}
              unidade="compromissos"
              rotuloItem="Desfecho"
            />
            <p className="m-0 text-suave">
              Realizados: {formatarDuracao(semana.minutosRealizados)} · {formatarInteiro(semana.individuais)} individuais + {formatarInteiro(semana.atendimentosRealizados - semana.individuais)}{" "}
              {semana.atendimentosRealizados - semana.individuais === 1 ? "reunião" : "reuniões"}
            </p>
            {p.porProfissional && (
              <div className="flex flex-col gap-2">
                <h3 className="m-0 text-sm font-semibold">Por profissional</h3>
                <TabelaPorProfissional dados={p.porProfissional} />
              </div>
            )}
          </div>
        )}
      </Modal>
      <Modal aberto={detalhe === "taxa"} aoFechar={() => setDetalhe(null)} titulo="Taxa de ausência">
        {semana && (
          <div className="flex flex-col gap-4 text-sm">
            {p.periodo.parcial && <p className="m-0 text-suave">Semana em andamento: os números contam só até agora.</p>}
            <GraficoAnel
              titulo="Base da taxa de ausência"
              descricao="Atendimentos individuais da semana divididos em realizados e faltas. Reuniões não entram na conta."
              dados={fatiasAusencia(semana)}
              unidade="individuais"
              rotuloItem="Desfecho"
            />
            <p className="m-0 text-suave">
              {semana.taxaAusencia == null ? (
                <>Taxa não calculada: {semana.motivoNaoCalculado ?? "sem base na semana"}.</>
              ) : (
                <>
                  Taxa de ausência: <strong className="text-texto tabular-nums">{formatarPercentual(semana.taxaAusencia, 1)}</strong> ={" "}
                  {formatarInteiro(semana.faltas)} {semana.faltas === 1 ? "falta" : "faltas"} ÷ {formatarInteiro(semana.individuais + semana.faltas)} individuais · reuniões não entram na conta
                </>
              )}
            </p>
          </div>
        )}
      </Modal>
      <Modal aberto={detalhe === "meses"} aoFechar={() => setDetalhe(null)} titulo={tituloMeses}>
        {meses && (
          <div className="flex flex-col gap-4 text-sm">
            {mesParcial && <p className="m-0 text-suave">{mesParcial.rotulo} é parcial: conta só até hoje.</p>}
            <GraficoBarras
              titulo={tituloMeses}
              descricao={`Atendimentos realizados por mês, de ${meses[0]?.rotulo} a ${meses.at(-1)?.rotulo}. Ausências não contam.`}
              dados={meses}
              unidade="Realizados"
              rotuloItem="Mês"
              orientacao="vertical"
              className="w-full"
              tabelaVisivel
            />
          </div>
        )}
      </Modal>
      <Modal aberto={detalhe === "cursos"} aoFechar={() => setDetalhe(null)} titulo="Alunos por curso">
        {cursos && (
          <div className="flex flex-col gap-4 text-sm">
            <p className="m-0 text-suave">{formatarInteiro(totalCursos)} alunos acompanhados, por curso atual.</p>
            <GraficoBarras titulo="Alunos por curso" descricao="Alunos acompanhados por curso atual." dados={cursos} unidade="Alunos" rotuloItem="Curso" orientacao="horizontal" className="w-full" tabelaVisivel />
          </div>
        )}
      </Modal>
    </>
  );
}
