import { Download } from "lucide-react";
import { useId, useState } from "react";
import { useSearchParams } from "react-router";
import { ehCoordenacao, pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { Button } from "../../components/ui/Button";
import { Alert } from "../../components/ui/Alert";
import { Card, classeTituloPagina } from "../../components/ui/Card";
import { CartaoSecao as Secao } from "../../components/ui/Secao";
import { Carregando } from "../../components/ui/Carregando";
import { ErrorState } from "../../components/ui/ErrorState";
import { ListaAbas } from "../../components/ui/ListaAbas";
import { Select } from "../../components/ui/Select";
import { Skeleton } from "../../components/ui/Skeleton";
import { Table } from "../../components/ui/Table";
import { useToast } from "../../components/ui/Toast";
import { mensagemErro } from "../../lib/api";
import { cn } from "../../lib/cn";
import { DIA, formatarInteiro, formatarPercentual, hojeCivil } from "../../lib/format";
import type { Producao } from "../../lib/types";
import { useCursos, useProfissionais } from "../compartilhado/catalogos";
import { AjustesTab } from "./components/AjustesTab";
import { ComparacaoProfissionais } from "./components/ComparacaoProfissionais";
import { GraficoSerie } from "./components/GraficoSerie";
import { QuadroTempo } from "./components/QuadroTempo";
import { SemanasTab } from "./components/SemanasTab";
import { ROTULO_PERIODO, SeletorPeriodo, type TipoPeriodo } from "./components/SeletorPeriodo";
import { minutosComSinal, TabelaCategorias } from "./components/TabelaCategorias";
import { useProducao } from "./hooks/useProducao";
import { producaoService, type FormatoExportacao } from "./services/producaoService";

type Aba = "visao" | "semanas" | "ajustes";
const TODAS_ABAS: { chave: Aba; rotulo: string }[] = [
  { chave: "visao", rotulo: "Visão geral" },
  { chave: "semanas", rotulo: "Semanas" },
  { chave: "ajustes", rotulo: "Ajustes" },
];

/** Variação absoluta e percentual sobre o período anterior; sem base (percentual nulo) mostra "—". */
function ComparacaoAnterior({ producao }: { producao: Producao }) {
  const rotulo = (chave: string) => producao.categorias.find((c) => c.chave === chave)?.rotulo ?? chave;
  const sinal = (n: number) => (n > 0 ? `+${formatarInteiro(n)}` : formatarInteiro(n));
  const pct = (f: number | null) => (f == null ? "—" : formatarPercentual(f, 0));
  return (
    <Table caption="Variação sobre o período anterior">
      <thead>
        <tr className="border-b border-borda text-xs text-suave">
          {["Categoria", "Ocorrências", "Variação", "Tempo", "Variação"].map((r, i) => (
            <th key={i} scope="col" className={i === 0 ? "py-1 font-semibold" : "py-1 pl-3 text-right font-semibold"}>{r}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {producao.anterior.categorias.map((v) => (
          <tr key={v.chave} className="border-b border-borda">
            <th scope="row" className="py-1 pr-2 font-normal">{rotulo(v.chave)}</th>
            <td className="py-1 pl-3 text-right tabular-nums">{v.anterior.ocorrencias == null ? "—" : formatarInteiro(v.anterior.ocorrencias)}</td>
            <td className="py-1 pl-3 text-right tabular-nums">
              {v.diferenca.ocorrencias == null ? "—" : `${sinal(v.diferenca.ocorrencias)} (${pct(v.percentualOcorrencias)})`}
            </td>
            <td className="py-1 pl-3 text-right tabular-nums">{minutosComSinal(v.anterior.minutos)}</td>
            <td className="py-1 pl-3 text-right tabular-nums">
              {v.diferenca.minutos > 0 ? "+" : ""}
              {minutosComSinal(v.diferenca.minutos)} ({pct(v.percentualMinutos)})
            </td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}

export function ProducaoPage() {
  const { usuario } = useSessao();
  const [params, setParams] = useSearchParams();
  const hoje = hojeCivil();
  const coordenacao = ehCoordenacao(usuario);
  const gestao = usuario?.perfil === "Gestao";
  const idFiltro = useId();
  const idEvolucao = useId();
  const profissionais = useProfissionais(coordenacao || gestao, true);
  const cursos = useCursos(coordenacao);

  const tipoUrl = params.get("tipo");
  const tipo: TipoPeriodo = tipoUrl && tipoUrl in ROTULO_PERIODO ? (tipoUrl as TipoPeriodo) : "Mes";
  const refUrl = params.get("referencia") ?? "";
  const referencia = DIA.test(refUrl) && !Number.isNaN(Date.parse(refUrl)) ? refUrl : hoje;
  // Só coordenação e gestão escolhem profissional; só coordenação escolhe curso. Para os demais a API força o recorte.
  const profissionalId = coordenacao || gestao ? params.get("profissionalId") || undefined : undefined;
  const cursoId = coordenacao ? params.get("cursoId") || undefined : undefined;
  // Semanas: só Equipe NAP (a capacidade "agenda" é dela) e, para a coordenação sem profissional escolhido, só semana ou mês:
  // a lista traz uma linha por profissional e semana (ano × equipe inteira passa de mil linhas). Ajustes: só coordenação (producao.gerir).
  const semanasCabem = !coordenacao || !!profissionalId || tipo === "Semana" || tipo === "Mes";
  const abas = TODAS_ABAS.filter((a) => a.chave === "visao" || (a.chave === "semanas" ? pode(usuario, "agenda") && semanasCabem : pode(usuario, "producao.gerir")));
  const aba = abas.find((a) => a.chave === params.get("aba")) ?? abas[0]!;
  const consulta = useProducao({ tipo, referencia, profissionalId, cursoId });
  const toast = useToast();
  const [exportando, setExportando] = useState<FormatoExportacao | null>(null);

  async function exportar(formato: FormatoExportacao) {
    setExportando(formato);
    try {
      await producaoService.exportar(formato, { tipo, referencia, profissionalId, cursoId });
    } catch (e) {
      toast.erro(mensagemErro(e));
    } finally {
      setExportando(null);
    }
  }

  function alterar(mudancas: Record<string, string>) {
    setParams(
      (p) => {
        const proximo = new URLSearchParams(p);
        for (const [k, v] of Object.entries(mudancas)) {
          if (v) proximo.set(k, v);
          else proximo.delete(k);
        }
        return proximo;
      },
      { replace: true },
    );
  }

  const p = consulta.data;
  const exportacao =
    p && aba.chave === "visao"
      ? (["pdf", "xlsx"] as const).map((f) => (
          <Button key={f} tamanho="sm" variante="secundaria" disabled={exportando !== null} carregando={exportando === f} onClick={() => void exportar(f)}>
            <Download aria-hidden="true" className="size-4" />
            {f === "pdf" ? "Exportar PDF" : "Exportar Excel"}
          </Button>
        ))
      : null;
  const taxa =
    p &&
    (p.taxaAusencia.valor == null ? (
      p.taxaAusencia.motivo
    ) : (
      <>
        <strong className="font-semibold tabular-nums">{formatarPercentual(p.taxaAusencia.valor, 1)}</strong>
        <span className="text-suave">
          {" "}
          · {formatarInteiro(p.taxaAusencia.numerador)} de {formatarInteiro(p.taxaAusencia.denominador)} atendimentos individuais + faltas
        </span>
      </>
    ));
  const classeFiltro = "min-h-9 w-auto flex-auto rounded-full py-0 text-rotulo sm:flex-none";
  // Ao trocar período ou filtro os números anteriores ficam na tela, esmaecidos, até a resposta chegar (como no painel).
  const ocupado = consulta.isPlaceholderData || undefined;
  const classeOcupado = cn("transition-opacity", ocupado && "opacity-60");
  return (
    <>
      <title>Produção · NAP</title>
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className={classeTituloPagina}>Produção</h1>
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          {(coordenacao || gestao) && (
            <>
              <label htmlFor={`${idFiltro}-prof`} className="sr-only">
                Profissional
              </label>
              <Select id={`${idFiltro}-prof`} value={profissionalId ?? ""} onChange={(e) => alterar({ profissionalId: e.target.value })} className={classeFiltro}>
                <option value="">Profissional: toda a equipe</option>
                {profissionais.data?.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.ativo ? x.nome : `${x.nome} (inativo)`}
                  </option>
                ))}
              </Select>
            </>
          )}
          {coordenacao && (
            <>
              <label htmlFor={`${idFiltro}-curso`} className="sr-only">
                Curso
              </label>
              <Select id={`${idFiltro}-curso`} value={cursoId ?? ""} onChange={(e) => alterar({ cursoId: e.target.value })} className={classeFiltro}>
                <option value="">Curso: todos</option>
                {cursos.data?.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nome}
                  </option>
                ))}
              </Select>
            </>
          )}
          <SeletorPeriodo tipo={tipo} referencia={referencia} hoje={hoje} periodo={p?.periodo} aoMudar={(t, r) => alterar({ tipo: t, referencia: r })} />
        </div>
      </header>

      {abas.length > 1 ? (
        // Exportação na linha das abas; no celular os botões quebram para baixo, sem a linha de base.
        <div className="flex flex-wrap items-end justify-end gap-y-2">
          <div className="min-w-0 grow basis-64">
            <ListaAbas abas={abas} atual={aba.chave} aoTrocar={(chave) => alterar({ aba: chave === "visao" ? "" : chave })} rotulo="Áreas da produção" />
          </div>
          {exportacao && <div className="flex flex-wrap justify-end gap-2 pl-2 lg:border-b lg:border-borda lg:pb-1.5">{exportacao}</div>}
        </div>
      ) : (
        exportacao && <div className="flex flex-wrap justify-end gap-2">{exportacao}</div>
      )}

      {consulta.isPending && (
        <Carregando rotulo="Carregando produção" className="flex flex-col gap-3">
          <Skeleton className="h-56 rounded-cartao" />
          <Skeleton className="h-32 rounded-cartao" />
        </Carregando>
      )}
      {consulta.isError && <ErrorState erro={consulta.error} aoTentarNovamente={() => void consulta.refetch()} />}
      {p && aba.chave === "semanas" && (
        <div role="tabpanel" id="painel-semanas" aria-labelledby="aba-semanas" aria-busy={ocupado} className={cn("md:flex md:min-h-0 md:flex-1 md:flex-col", classeOcupado)}>
          <SemanasTab key={`${p.periodo.inicio}-${profissionalId ?? ""}`} inicio={p.periodo.inicio} fim={p.periodo.fim} profissionalId={profissionalId} hoje={hoje} podeReabrir={pode(usuario, "producao.gerir")} />
        </div>
      )}
      {p && aba.chave === "ajustes" && (
        <div role="tabpanel" id="painel-ajustes" aria-labelledby="aba-ajustes" aria-busy={ocupado} className={cn("md:flex md:min-h-0 md:flex-1 md:flex-col", classeOcupado)}>
          <AjustesTab key={`${p.periodo.inicio}-${profissionalId ?? ""}`} inicio={p.periodo.inicio} fim={p.periodo.fim} profissionalId={profissionalId} />
        </div>
      )}
      {p && aba.chave === "visao" && (
        <div role={abas.length > 1 ? "tabpanel" : undefined} id="painel-visao" aria-labelledby={abas.length > 1 ? "aba-visao" : undefined} aria-busy={ocupado} className={cn("grid gap-3 2xl:grid-cols-10", classeOcupado)}>
          {/* A partir do 2xl as seções formam duas colunas (categoria | tempo + evolução; anterior | profissionais) para a visão caber na tela;
              a tabela de categorias, a mais larga, leva 6/10. */}
          {p.periodo.parcial && <Alert variante="aguardando" className="2xl:col-span-full">Período parcial: os números crescem até o fim do período, e dias futuros não entram na capacidade.</Alert>}
          {cursoId && <Alert variante="info" className="2xl:col-span-full">Visão do curso: capacidade e tempo não se aplicam a um curso, só ocorrências e atendimentos dos alunos dele.</Alert>}
          <Secao titulo="Produção por categoria" className="2xl:col-span-6">
            <TabelaCategorias categorias={p.categorias} />
          </Secao>
          {/* Em tela larga, o quadro de tempo (com a taxa de ausência) e a evolução ficam lado a lado. */}
          <div className="grid gap-3 xl:grid-cols-2 2xl:col-span-4 2xl:grid-cols-1">
            {p.tempo ? (
              <Secao titulo="Capacidade e tempo">
                <QuadroTempo tempo={p.tempo} pendentes={p.pendentes} taxaAusencia={taxa} />
              </Secao>
            ) : (
              <Secao titulo="Taxa de ausência">
                <p className="m-0 text-sm">{taxa}</p>
              </Secao>
            )}
            {p.serie.length > 0 && (
              <Card aria-labelledby={idEvolucao}>
                <GraficoSerie producao={p} idTitulo={idEvolucao} />
              </Card>
            )}
          </div>
          <Secao titulo="Comparação com o período anterior" className="2xl:col-span-5">
            <ComparacaoAnterior producao={p} />
          </Secao>
          {p.porProfissional && p.porProfissional.length > 0 && (
            <Secao titulo="Comparação entre profissionais" className="2xl:col-span-5">
              <ComparacaoProfissionais linhas={p.porProfissional} />
            </Secao>
          )}
        </div>
      )}
    </>
  );
}
