import { useQuery } from "@tanstack/react-query";
import { CalendarDays, ChevronLeft, ChevronRight, Plus } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import { ehCoordenacao, pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { Alert } from "../../components/ui/Alert";
import { Button } from "../../components/ui/Button";
import { classeIconeTitulo, classeTituloPagina } from "../../components/ui/Card";
import { ErrorState } from "../../components/ui/ErrorState";
import { Modal } from "../../components/ui/Modal";
import { Select } from "../../components/ui/Select";
import { Skeleton } from "../../components/ui/Skeleton";
import { useToast } from "../../components/ui/Toast";
import { cn } from "../../lib/cn";
import { DIA, diaCivil, formatarDiaSemana, formatarSemana, hojeCivil, segundaDoDia, somarDias } from "../../lib/format";
import { queryKeys } from "../../lib/queryKeys";
import type { AtendimentoAvulsoAgenda, AtividadeAvulsaAgenda, Compromisso } from "../../lib/types";
import { alunoService } from "../alunos/services/alunoService";
import { AtendimentoForm } from "../atendimentos/components/AtendimentoForm";
import { AtividadeForm } from "../atividades/components/AtividadeForm";
import { useProfissionais } from "../compartilhado/catalogos";
import { CompromissoForm } from "./components/CompromissoForm";
import { DetalheAtividadeAvulsa, DetalheAvulso, DetalheCompromisso } from "./components/DetalheCompromisso";
import { datasDosItens, diasVisiveis, GradeSemanal } from "./components/GradeSemanal";
import { LegendaAgenda } from "./components/LegendaAgenda";
import { useEscalaSemana, useSemana } from "./hooks/useAgenda";

const EQUIPE = "equipe";

/** Relógio da linha "agora": atualiza a cada minuto. */
function useAgora(): Date {
  const [agora, setAgora] = useState(() => new Date());
  useEffect(() => {
    const t = setInterval(() => setAgora(new Date()), 60_000);
    return () => clearInterval(t);
  }, []);
  return agora;
}

function GradeCarregando() {
  return (
    <div aria-hidden="true" className="grid flex-1 grid-cols-[48px_minmax(0,1fr)] grid-rows-[auto_1fr] xl:grid-cols-[48px_repeat(5,minmax(0,1fr))]">
      <div className="border-b border-borda" />
      {Array.from({ length: 5 }, (_, i) => (
        <div key={i} className={cn("border-b border-l border-borda px-2.5 py-2.5", i > 0 && "max-xl:hidden")}>
          <Skeleton className="h-4 w-14" />
        </div>
      ))}
      <div className="min-h-[600px]" />
      {Array.from({ length: 5 }, (_, i) => (
        <div key={i} className={cn("relative min-h-[600px] border-l border-borda", i > 0 && "max-xl:hidden")}>
          <Skeleton className="absolute inset-x-1 top-[60px] h-[58px]" />
          <Skeleton className="absolute inset-x-1 top-[180px] h-[88px]" />
          {i % 2 === 0 && <Skeleton className="absolute inset-x-1 top-[420px] h-[58px]" />}
        </div>
      ))}
    </div>
  );
}

export function AgendaPage() {
  const { usuario } = useSessao();
  const coordenacao = ehCoordenacao(usuario);
  const toast = useToast();
  const [params, setParams] = useSearchParams();
  const agora = useAgora();
  const hoje = hojeCivil(agora);

  const semanaUrl = params.get("semana") ?? "";
  // Sempre a segunda-feira no calendário de Cuiabá: a API recusa outro dia.
  const segunda = segundaDoDia(DIA.test(semanaUrl) && !Number.isNaN(Date.parse(semanaUrl)) ? semanaUrl : hoje);
  // Todos abrem na própria agenda: com a equipe inteira (uma pista por profissional em cada dia) a grade só é legível em tela larga, e por escolha.
  const visao = params.get("profissional") ?? usuario?.id ?? EQUIPE;
  const daEquipe = visao === EQUIPE;
  const profissionalId = daEquipe ? undefined : visao;
  const alunoUrl = params.get("aluno");

  const escala = useEscalaSemana(segunda, profissionalId);
  const semana = useSemana(segunda, profissionalId);
  const profissionais = useProfissionais();
  const fichaAluno = useQuery({
    queryKey: queryKeys.alunos.ficha(alunoUrl ?? ""),
    queryFn: () => alunoService.obter(alunoUrl ?? ""),
    enabled: !!alunoUrl,
  });

  const [novo, setNovo] = useState(false);
  const [compromissoAberto, setCompromissoAberto] = useState<Compromisso | null>(null);
  const [avulsoAberto, setAvulsoAberto] = useState<AtendimentoAvulsoAgenda | null>(null);
  const [atividadeAberta, setAtividadeAberta] = useState<AtividadeAvulsaAgenda | null>(null);
  const [registrando, setRegistrando] = useState<Compromisso | null>(null);
  const [registrandoAtividade, setRegistrandoAtividade] = useState<Compromisso | null>(null);
  const indiceHoje = [0, 1, 2, 3, 4].find((i) => somarDias(segunda, i) === hoje);
  const [diaMovel, setDiaMovel] = useState(indiceHoje ?? 0);

  function alterarUrl(mudar: (p: URLSearchParams) => void) {
    setParams((atual) => {
      const proximo = new URLSearchParams(atual);
      mudar(proximo);
      return proximo;
    });
  }

  /** Visão de uma pessoa: passa a exibir quem recebeu o compromisso, para ele não sumir da tela (e ser recriado em duplicidade). */
  function exibirProfissional(p: URLSearchParams, efetivo: string | undefined) {
    if (!daEquipe && efetivo) p.set("profissional", efetivo);
  }

  /** Uma única atualização da URL por ação: chamadas seguidas de setParams no mesmo tick se sobrescrevem. */
  function irParaSemana(dia: string, tambem?: (p: URLSearchParams) => void) {
    const destino = segundaDoDia(dia);
    alterarUrl((p) => {
      if (destino === segundaDoDia(hoje)) p.delete("semana");
      else p.set("semana", destino);
      tambem?.(p);
    });
    // Sempre pela diferença até a segunda de destino: indiceHoje é da semana em tela, não da semana de destino.
    setDiaMovel(Math.max(0, Math.round((Date.parse(dia) - Date.parse(destino)) / 86_400_000)));
  }

  function fecharNovo() {
    setNovo(false);
    alterarUrl((p) => p.delete("aluno"));
  }

  // Sábado/domingo só existem quando a escala tem faixa; o dia exibido no celular nunca passa do último.
  const dias = escala.data ? diasVisiveis(escala.data, datasDosItens([...(semana.data?.compromissos ?? []), ...(semana.data?.atendimentosAvulsos ?? []), ...(semana.data?.atividadesAvulsas ?? [])])) : [];
  const diaAtivo = Math.min(diaMovel, Math.max(dias.length - 1, 0));
  const formularioAberto = novo || !!alunoUrl;
  const dados = semana.data;
  // Semana carregada por último: a grade nova desliza no sentido da navegação; com dados anteriores em tela (placeholder) nada se move.
  const semanaCarregada = useRef<string | undefined>(undefined);
  const direcao = dados && semanaCarregada.current ? Math.sign(dados.inicio.localeCompare(semanaCarregada.current)) : 0;
  useEffect(() => {
    semanaCarregada.current = dados?.inicio;
  }, [dados?.inicio]);
  const semCompromissos = !!dados && !semana.isPlaceholderData && dados.compromissos.length === 0 && dados.atendimentosAvulsos.length === 0 && dados.atividadesAvulsas.length === 0;

  let corpo;
  if (escala.isError || (semana.isError && !dados)) {
    corpo = (
      <div className="p-4">
        <ErrorState
          erro={escala.error ?? semana.error}
          aoTentarNovamente={() => {
            if (escala.isError) void escala.refetch();
            if (semana.isError) void semana.refetch();
          }}
        />
      </div>
    );
  } else if (!escala.data || !dados) {
    corpo = <GradeCarregando />;
  } else {
    corpo = (
      <div
        key={dados.inicio}
        aria-busy={semana.isPlaceholderData}
        className={cn(
          "flex min-h-0 flex-1 flex-col overflow-y-auto transition-[translate,opacity] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] starting:opacity-0 motion-reduce:transition-opacity",
          direcao > 0 && "starting:translate-x-6",
          direcao < 0 && "starting:-translate-x-6",
          semana.isPlaceholderData && "opacity-60",
        )}
      >
        {semCompromissos && (
          <p className="border-b border-borda px-4 py-2.5 text-denso text-suave">
            Nenhum compromisso nesta semana. Use “Novo compromisso” para agendar.
          </p>
        )}
        <GradeSemanal
          escala={escala.data}
          compromissos={dados.compromissos}
          avulsos={dados.atendimentosAvulsos}
          atividades={dados.atividadesAvulsas}
          agora={agora}
          equipe={daEquipe ? (profissionais.data ?? []) : undefined}
          diaMovel={diaAtivo}
          aoAbrirCompromisso={setCompromissoAberto}
          aoAbrirAvulso={setAvulsoAberto}
          aoAbrirAtividade={setAtividadeAberta}
        />
      </div>
    );
  }

  return (
    <>
      <title>Agenda · NAP</title>
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          {/* No celular o ícone decorativo sai: título e navegação da semana dividem a mesma linha. */}
          <span aria-hidden="true" className={cn(classeIconeTitulo, "max-sm:hidden")}>
            <CalendarDays className="size-5" />
          </span>
          <div className="mr-1.5">
            <p className="text-xs text-suave">Agenda semanal da equipe</p>
            <h1 aria-live="polite" className={classeTituloPagina}>
              <span className="sr-only">Agenda: </span>
              {formatarSemana(segunda)}
            </h1>
          </div>
          {/* Navegação da semana quebra de linha como um bloco: "Hoje" não fica sozinho no celular. */}
          <div className="flex items-center gap-2">
            <Button tamanho="icone" variante="secundaria" aria-label="Semana anterior" title="Semana anterior" onClick={() => irParaSemana(somarDias(segunda, -7))}>
              <ChevronLeft aria-hidden="true" className="size-4" />
            </Button>
            <Button tamanho="icone" variante="secundaria" aria-label="Próxima semana" title="Próxima semana" onClick={() => irParaSemana(somarDias(segunda, 7))}>
              <ChevronRight aria-hidden="true" className="size-4" />
            </Button>
            <Button variante="secundaria" onClick={() => irParaSemana(hoje)} disabled={segunda === segundaDoDia(hoje)}>
              Hoje
            </Button>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2 max-sm:w-full">
          <label className="sr-only" htmlFor="agenda-visao">
            Agenda exibida
          </label>
          <Select
            id="agenda-visao"
            className="w-auto min-w-0 truncate max-sm:flex-1"
            value={visao}
            onChange={(e) => alterarUrl((p) => p.set("profissional", e.target.value))}
          >
            <option value={EQUIPE}>Toda a equipe</option>
            {usuario && !profissionais.data?.some((p) => p.id === usuario.id) && <option value={usuario.id}>{usuario.nome}</option>}
            {profissionais.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nome}
              </option>
            ))}
          </Select>
          <Button className="rounded-full" onClick={() => setNovo(true)}>
            <Plus aria-hidden="true" className="size-4" />
            Novo compromisso
          </Button>
        </div>
      </header>

      {escala.data && (
        <div className="flex gap-1.5 xl:hidden" role="group" aria-label="Dia exibido">
          {dias.map((d, i) => {
            const dia = d.data;
            return (
              <button
                key={dia}
                type="button"
                aria-pressed={i === diaAtivo}
                onClick={() => setDiaMovel(i)}
                className={cn(
                  "flex-1 rounded-lg border py-2 text-rotulo font-semibold transition-colors duration-150 pointer-coarse:min-h-11",
                  i === diaAtivo ? "border-primaria bg-primaria text-primaria-texto" : "border-borda bg-superficie text-texto",
                )}
              >
                {formatarDiaSemana(dia)}
              </button>
            );
          })}
        </div>
      )}

      {/* No desktop a seção ocupa o resto da altura do main: a grade estica até a legenda e, se for mais alta, rola por dentro. */}
      <section aria-label="Grade semanal" className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-secao border border-borda bg-superficie">
        {corpo}
        <LegendaAgenda equipe={daEquipe} agora={agora} escala={escala.data} />
      </section>

      <Modal aberto={formularioAberto} aoFechar={fecharNovo} titulo="Novo compromisso">
        {alunoUrl && fichaAluno.isPending ? (
          <Skeleton className="h-40 w-full" />
        ) : (
          <>
            {alunoUrl && fichaAluno.isError && (
              <Alert variante="urgente" className="mb-4">
                Não foi possível carregar o aluno indicado. Busque-o no campo Aluno.
              </Alert>
            )}
            <CompromissoForm
              coordenacao={coordenacao}
              usuarioId={usuario?.id}
              profissionalPadrao={profissionalId ?? usuario?.id}
              dataPadrao={segunda === segundaDoDia(hoje) && indiceHoje !== undefined ? hoje : segunda}
              alunoInicial={fichaAluno.data ? { id: fichaAluno.data.id, nome: fichaAluno.data.nome, matricula: fichaAluno.data.matricula } : undefined}
              aoCancelar={fecharNovo}
              aoConcluir={(_id, inicio, profissional) => {
                toast.sucesso("Compromisso agendado.");
                setNovo(false);
                irParaSemana(diaCivil(inicio), (p) => {
                  p.delete("aluno");
                  exibirProfissional(p, profissional ?? usuario?.id);
                });
              }}
            />
          </>
        )}
      </Modal>

      {compromissoAberto && (
        <DetalheCompromisso
          key={compromissoAberto.id}
          compromisso={compromissoAberto}
          podeAgir={coordenacao || compromissoAberto.profissionalId === usuario?.id}
          propria={compromissoAberto.profissionalId === usuario?.id}
          aoFechar={() => setCompromissoAberto(null)}
          aoRemarcado={(_id, inicio) => {
            const profissional = compromissoAberto.profissionalId;
            setCompromissoAberto(null);
            irParaSemana(diaCivil(inicio), (p) => exibirProfissional(p, profissional));
          }}
          aoRegistrarAtendimento={(c) => {
            setCompromissoAberto(null);
            setRegistrando(c);
          }}
          aoRegistrarAtividade={
            pode(usuario, "atividades.registrar")
              ? (c) => {
                  setCompromissoAberto(null);
                  setRegistrandoAtividade(c);
                }
              : undefined
          }
        />
      )}
      <Modal aberto={!!registrando} aoFechar={() => setRegistrando(null)} titulo="Registrar atendimento">
        {registrando && (
          <AtendimentoForm
            key={registrando.id}
            compromisso={registrando}
            aoCancelar={() => setRegistrando(null)}
            aoConcluir={() => {
              toast.sucesso("Atendimento registrado.");
              setRegistrando(null);
            }}
          />
        )}
      </Modal>
      <Modal aberto={!!registrandoAtividade} aoFechar={() => setRegistrandoAtividade(null)} titulo="Registrar atividade">
        {registrandoAtividade && (
          <AtividadeForm
            key={registrandoAtividade.id}
            compromisso={registrandoAtividade}
            aoCancelar={() => setRegistrandoAtividade(null)}
            aoConcluir={() => {
              toast.sucesso("Atividade registrada.");
              setRegistrandoAtividade(null);
            }}
          />
        )}
      </Modal>
      {avulsoAberto && <DetalheAvulso atendimento={avulsoAberto} aoFechar={() => setAvulsoAberto(null)} />}
      {atividadeAberta && <DetalheAtividadeAvulsa atividade={atividadeAberta} aoFechar={() => setAtividadeAberta(null)} />}
    </>
  );
}
