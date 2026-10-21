import { useQuery } from "@tanstack/react-query";
import { ClipboardList, Plus, X } from "lucide-react";
import { useId, useState } from "react";
import { useSearchParams } from "react-router";
import { Button } from "../../components/ui/Button";
import { classeIconeTitulo, classeTituloPagina } from "../../components/ui/Card";
import { classeData, classeFiltro, Input } from "../../components/ui/Input";
import { Modal } from "../../components/ui/Modal";
import { Select } from "../../components/ui/Select";
import { classeMolduraRolavel } from "../../components/ui/Table";
import { useToast } from "../../components/ui/Toast";
import { cn } from "../../lib/cn";
import { DIA } from "../../lib/format";
import { comParametro, paginaDaUrl, queryKeys } from "../../lib/queryKeys";
import { BuscaAluno } from "../alunos/components/BuscaAluno";
import { alunoService } from "../alunos/services/alunoService";
import { estiloAtendimento, ROTULO_TIPO_ATENDIMENTO } from "../agenda/components/estiloCompromisso";
import { useProfissionais } from "../compartilhado/catalogos";
import { AtendimentoForm, ehTipo, TIPOS_ATENDIMENTO } from "./components/AtendimentoForm";
import { DetalheAtendimento } from "./components/DetalheAtendimento";
import { ListaAtendimentos } from "./components/ListaAtendimentos";
import type { FiltroAtendimentos } from "./services/atendimentoService";

const ESTADOS = ["Realizado", "Ausente", "Remarcado", "Cancelado"] as const;
const CHAVES_FILTRO = ["alunoId", "profissionalId", "estado", "tipo", "de", "ate"] as const;

const NOTA_ATENDIMENTOS =
  "Ausência não exige demanda ou nota clínica. Observações privadas abrem só dentro do atendimento. Correções da coordenação são justificadas e auditadas.";

/** A URL é a fonte da verdade dos filtros (recarregar, voltar e compartilhar o link mantêm a lista). */
function filtroDaUrl(params: URLSearchParams): FiltroAtendimentos {
  const f: FiltroAtendimentos = {};
  const texto = (c: string) => params.get(c)?.trim() || undefined;
  f.alunoId = texto("alunoId");
  f.profissionalId = texto("profissionalId");
  const estado = texto("estado");
  if (ESTADOS.some((e) => e === estado)) f.estado = estado as (typeof ESTADOS)[number];
  const tipo = texto("tipo") ?? "";
  if (ehTipo(tipo)) f.tipo = tipo;
  const de = texto("de");
  if (de && DIA.test(de)) f.de = de;
  const ate = texto("ate");
  if (ate && DIA.test(ate)) f.ate = ate;
  f.pagina = paginaDaUrl(params);
  return f;
}

export function AtendimentosPage() {
  const [params, setParams] = useSearchParams();
  const toast = useToast();
  const id = useId();
  const profissionais = useProfissionais();
  const [registrando, setRegistrando] = useState(false);
  const filtro = filtroDaUrl(params);
  const comFiltro = CHAVES_FILTRO.some((c) => params.get(c)?.trim());
  const aberto = params.get("id");
  // O nome do aluno filtrado vem da ficha (o link pode ter só o id).
  const alunoFiltro = useQuery({
    queryKey: queryKeys.alunos.ficha(filtro.alunoId ?? ""),
    queryFn: () => alunoService.obter(filtro.alunoId ?? ""),
    enabled: !!filtro.alunoId,
  });

  const alterar = (chave: (typeof CHAVES_FILTRO)[number] | "pagina" | "id", valor: string | null) =>
    setParams((atual) => comParametro(atual, chave, valor), { replace: chave !== "id" });

  return (
    <>
      <title>Atendimentos · NAP</title>
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className={cn(classeTituloPagina, "flex items-center gap-3")}>
          <span aria-hidden="true" className={classeIconeTitulo}>
            <ClipboardList className="size-5" />
          </span>
          Atendimentos
        </h1>
        {/* No celular só o ícone, na linha do título; o texto continua como nome acessível. */}
        <Button className="rounded-full max-sm:px-3" title="Registrar atendimento" onClick={() => setRegistrando(true)}>
          <Plus aria-hidden="true" className="size-4" />
          <span className="max-sm:sr-only">Registrar atendimento</span>
        </Button>
      </header>

      <div role="search" aria-label="Filtrar atendimentos" className="flex flex-wrap items-center gap-2">
        <div className="min-w-[240px] flex-1">
          {filtro.alunoId ? (
            <div className="flex min-h-[2.375rem] items-center justify-between gap-2 rounded-campo border border-borda bg-realce px-3 text-sm">
              <span className="truncate">
                <span className="text-suave">Aluno: </span>
                <span className="font-semibold">{alunoFiltro.data?.nome ?? (alunoFiltro.isError ? "aluno indicado" : "carregando…")}</span>
              </span>
              <Button tamanho="sm" variante="fantasma" aria-label="Remover filtro de aluno" onClick={() => alterar("alunoId", null)}>
                <X aria-hidden="true" className="size-3.5" />
              </Button>
            </div>
          ) : (
            <BuscaAluno aluno={null} aoEscolher={(a) => alterar("alunoId", a?.id ?? null)} obrigatorio={false} compacto />
          )}
        </div>
        <label htmlFor={`${id}-profissional`} className="sr-only">
          Profissional
        </label>
        <Select id={`${id}-profissional`} value={filtro.profissionalId ?? ""} onChange={(e) => alterar("profissionalId", e.target.value)} className={classeFiltro}>
          <option value="">Profissional: todos</option>
          {profissionais.data?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.nome}
            </option>
          ))}
        </Select>
        <label htmlFor={`${id}-estado`} className="sr-only">
          Estado
        </label>
        <Select id={`${id}-estado`} value={filtro.estado ?? ""} onChange={(e) => alterar("estado", e.target.value)} className={classeFiltro}>
          <option value="">Estado: todos</option>
          {ESTADOS.map((e) => (
            <option key={e} value={e}>
              {estiloAtendimento(e).rotuloEstado}
            </option>
          ))}
        </Select>
        <label htmlFor={`${id}-tipo`} className="sr-only">
          Tipo
        </label>
        {/* A opção mais longa alargava o campo e empurrava as datas para outra linha. */}
        <Select id={`${id}-tipo`} value={filtro.tipo ?? ""} onChange={(e) => alterar("tipo", e.target.value)} className={cn(classeFiltro, "text-ellipsis sm:max-w-56")}>
          <option value="">Tipo: todos</option>
          {TIPOS_ATENDIMENTO.map((t) => (
            <option key={t} value={t}>
              {ROTULO_TIPO_ATENDIMENTO[t]}
            </option>
          ))}
        </Select>
        <div className="flex w-full min-w-0 flex-wrap items-center gap-1.5 text-denso text-suave sm:w-auto">
          <span className="flex flex-1 items-center gap-1.5">
            <label htmlFor={`${id}-de`}>De</label>
            <Input id={`${id}-de`} type="date" value={filtro.de ?? ""} onChange={(e) => alterar("de", e.target.value)} className={classeData} />
          </span>
          <span className="flex flex-1 items-center gap-1.5">
            <label htmlFor={`${id}-ate`}>até</label>
            <Input id={`${id}-ate`} type="date" value={filtro.ate ?? ""} onChange={(e) => alterar("ate", e.target.value)} className={classeData} />
          </span>
        </div>
      </div>

      <section aria-label="Lista de atendimentos" className={classeMolduraRolavel}>
        <ListaAtendimentos
          filtro={filtro}
          comFiltro={comFiltro}
          aoMudarPagina={(p) => alterar("pagina", p > 1 ? String(p) : null)}
          aoAbrir={(a) => alterar("id", a)}
          vazio={{
            icone: <ClipboardList />,
            titulo: "Nenhum atendimento registrado",
            descricao: "Registre atendimentos a partir da agenda ou como avulsos.",
            acao: (
              <Button tamanho="sm" onClick={() => setRegistrando(true)}>
                Registrar atendimento
              </Button>
            ),
            acaoFiltro: (
              <Button variante="secundaria" tamanho="sm" onClick={() => setParams({})}>
                Limpar filtros
              </Button>
            ),
          }}
        />
        <p className="m-0 px-5 pb-3 text-rotulo text-suave">{NOTA_ATENDIMENTOS}</p>
      </section>

      <Modal aberto={registrando} aoFechar={() => setRegistrando(false)} titulo="Registrar atendimento avulso">
        <AtendimentoForm
          aoCancelar={() => setRegistrando(false)}
          aoConcluir={() => {
            toast.sucesso("Atendimento registrado.");
            setRegistrando(false);
          }}
        />
      </Modal>
      {aberto && <DetalheAtendimento key={aberto} id={aberto} aoFechar={() => alterar("id", null)} />}
    </>
  );
}
