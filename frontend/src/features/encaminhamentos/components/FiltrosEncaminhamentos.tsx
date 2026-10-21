import { useQuery } from "@tanstack/react-query";
import { X } from "lucide-react";
import { useId } from "react";
import { useSearchParams } from "react-router";
import { pode } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Button } from "../../../components/ui/Button";
import { classeData, classeFiltro, Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { DIA } from "../../../lib/format";
import { paginaDaUrl, queryKeys } from "../../../lib/queryKeys";
import { BuscaAluno } from "../../alunos/components/BuscaAluno";
import { alunoService } from "../../alunos/services/alunoService";
import { useCursos } from "../../compartilhado/catalogos";
import { DESTINOS, ESPECIALIDADES, ESTADOS, ROTULO_DESTINO, ROTULO_ESPECIALIDADE, ROTULO_ESTADO } from "../rotulos";
import type { FiltroEncaminhamentos } from "../services/encaminhamentoService";

export const CHAVES_FILTRO = ["alunoId", "cursoId", "especialidade", "destino", "estado", "de", "ate", "semAcolhimentoNoPrazo"] as const;
type ChaveFiltro = (typeof CHAVES_FILTRO)[number];
const entre = <T extends string>(lista: readonly T[], v: string | undefined): T | undefined => lista.find((x) => x === v);

/** A URL é a fonte da verdade: filtros sobrevivem a recarregar, voltar e compartilhar o link. */
export function filtroDaUrl(params: URLSearchParams): FiltroEncaminhamentos {
  const texto = (c: ChaveFiltro) => params.get(c)?.trim() || undefined;
  const f: FiltroEncaminhamentos = {
    alunoId: texto("alunoId"),
    cursoId: texto("cursoId"),
    especialidade: entre(ESPECIALIDADES, texto("especialidade")),
    destino: entre(DESTINOS, texto("destino")),
    estado: entre(ESTADOS, texto("estado")),
    de: DIA.test(texto("de") ?? "") ? texto("de") : undefined,
    ate: DIA.test(texto("ate") ?? "") ? texto("ate") : undefined,
    semAcolhimentoNoPrazo: params.get("semAcolhimentoNoPrazo") === "1" || undefined,
  };
  f.pagina = paginaDaUrl(params);
  return f;
}

export function FiltrosEncaminhamentos() {
  const [params, setParams] = useSearchParams();
  const { usuario } = useSessao();
  const cursos = useCursos();
  const id = useId();
  const alunoId = params.get("alunoId") ?? "";
  // O SIP não consulta alunos: sem busca nem nome do aluno (a API recusaria).
  const consultaAlunos = pode(usuario, "alunos.consultar");
  const alunoFiltro = useQuery({
    queryKey: queryKeys.alunos.ficha(alunoId),
    queryFn: () => alunoService.obter(alunoId),
    enabled: !!alunoId && consultaAlunos,
  });

  function atualizar(chave: ChaveFiltro, valor: string) {
    setParams(
      (atual) => {
        const proximo = new URLSearchParams(atual);
        if (valor) proximo.set(chave, valor);
        else proximo.delete(chave);
        proximo.delete("pagina");
        return proximo;
      },
      { replace: true },
    );
  }

  return (
    <div role="search" aria-label="Filtrar encaminhamentos" className="flex flex-wrap items-center gap-2">
      {alunoId ? (
        <div className="flex min-h-[2.375rem] min-w-[240px] flex-1 items-center justify-between gap-2 rounded-campo border border-borda bg-realce px-3 text-sm">
          <span className="truncate">
            <span className="text-suave">Aluno: </span>
            <span className="font-semibold">{alunoFiltro.data?.nome ?? (alunoFiltro.isPending && consultaAlunos ? "carregando…" : "aluno indicado")}</span>
          </span>
          <Button tamanho="sm" variante="fantasma" aria-label="Remover filtro de aluno" onClick={() => atualizar("alunoId", "")}>
            <X aria-hidden="true" className="size-3.5" />
          </Button>
        </div>
      ) : (
        consultaAlunos && (
          <div className="min-w-[240px] flex-1">
            <BuscaAluno aluno={null} aoEscolher={(a) => atualizar("alunoId", a?.id ?? "")} obrigatorio={false} compacto />
          </div>
        )
      )}
      {!usuario?.cursoId && (
        <>
          <label htmlFor={`${id}-curso`} className="sr-only">
            Curso
          </label>
          <Select id={`${id}-curso`} value={params.get("cursoId") ?? ""} onChange={(e) => atualizar("cursoId", e.target.value)} className={classeFiltro}>
            <option value="">Curso: todos</option>
            {cursos.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome}
              </option>
            ))}
          </Select>
        </>
      )}
      <label htmlFor={`${id}-especialidade`} className="sr-only">
        Especialidade
      </label>
      <Select id={`${id}-especialidade`} value={params.get("especialidade") ?? ""} onChange={(e) => atualizar("especialidade", e.target.value)} className={classeFiltro}>
        <option value="">Especialidade: todas</option>
        {ESPECIALIDADES.map((e) => (
          <option key={e} value={e}>
            {ROTULO_ESPECIALIDADE[e]}
          </option>
        ))}
      </Select>
      <label htmlFor={`${id}-destino`} className="sr-only">
        Destino
      </label>
      <Select id={`${id}-destino`} value={params.get("destino") ?? ""} onChange={(e) => atualizar("destino", e.target.value)} className={classeFiltro}>
        <option value="">Destino: todos</option>
        {DESTINOS.map((d) => (
          <option key={d} value={d}>
            {ROTULO_DESTINO[d]}
          </option>
        ))}
      </Select>
      <label htmlFor={`${id}-estado`} className="sr-only">
        Situação
      </label>
      <Select id={`${id}-estado`} value={params.get("estado") ?? ""} onChange={(e) => atualizar("estado", e.target.value)} className={classeFiltro}>
        <option value="">Situação: todas</option>
        {ESTADOS.map((e) => (
          <option key={e} value={e}>
            {ROTULO_ESTADO[e]}
          </option>
        ))}
      </Select>
      <div className="flex w-full min-w-0 flex-wrap items-center gap-1.5 text-denso text-suave sm:w-auto">
        <span className="flex flex-1 items-center gap-1.5">
          <label htmlFor={`${id}-de`} className="whitespace-nowrap">Enviado de</label>
          <Input id={`${id}-de`} type="date" value={params.get("de") ?? ""} onChange={(e) => atualizar("de", e.target.value)} className={classeData} />
        </span>
        <span className="flex flex-1 items-center gap-1.5">
          <label htmlFor={`${id}-ate`}>até</label>
          <Input id={`${id}-ate`} type="date" value={params.get("ate") ?? ""} onChange={(e) => atualizar("ate", e.target.value)} className={classeData} />
        </span>
      </div>
      <label className="inline-flex min-h-[2.375rem] cursor-pointer items-center gap-2 text-denso pointer-coarse:min-h-11">
        <input
          type="checkbox"
          checked={params.get("semAcolhimentoNoPrazo") === "1"}
          onChange={(e) => atualizar("semAcolhimentoNoPrazo", e.target.checked ? "1" : "")}
          className="size-4 accent-primaria"
        />
        Só sem acolhimento no prazo
      </label>
    </div>
  );
}
