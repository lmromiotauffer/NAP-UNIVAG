import { useId } from "react";
import { useSearchParams } from "react-router";
import { useSessao } from "../../../auth/useSessao";
import { BuscaUrl } from "../../../components/ui/BuscaUrl";
import { classeData, classeFiltro, Input } from "../../../components/ui/Input";
import { Select } from "../../../components/ui/Select";
import { paginaDaUrl } from "../../../lib/queryKeys";
import { useCursos, useProfissionais } from "../../compartilhado/catalogos";
import type { FiltroAlunosBody } from "../services/alunoService";
import { ESPECIALIDADES, ROTULO_ESPECIALIDADE } from "../../encaminhamentos/rotulos";
import { ROTULO_SITUACAO, SITUACOES, type Situacao } from "../rotulos";

export const CHAVES_FILTRO = ["busca", "cursoId", "profissionalId", "situacao", "inicioDe", "inicioAte", "especialidadeEncaminhamento"] as const;
type ChaveFiltro = (typeof CHAVES_FILTRO)[number];

/** A URL é a fonte da verdade: filtros sobrevivem a recarregar, voltar e compartilhar o link. */
export function filtroDaUrl(params: URLSearchParams): FiltroAlunosBody {
  const filtro: FiltroAlunosBody = {};
  for (const chave of CHAVES_FILTRO) {
    const valor = params.get(chave)?.trim();
    if (!valor) continue;
    if (chave === "situacao") {
      if ((SITUACOES as readonly string[]).includes(valor)) filtro.situacao = valor as Situacao;
    } else if (chave === "especialidadeEncaminhamento") {
      const especialidade = ESPECIALIDADES.find((x) => x === valor);
      if (especialidade) filtro.especialidadeEncaminhamento = especialidade;
    } else filtro[chave] = valor;
  }
  filtro.pagina = paginaDaUrl(params);
  return filtro;
}

export function FiltrosAlunos() {
  const [params, setParams] = useSearchParams();
  const { usuario } = useSessao();
  const cursos = useCursos();
  const profissionais = useProfissionais();
  const id = useId();
  const buscaUrl = params.get("busca") ?? "";

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

  // Coordenador de curso só enxerga encaminhamentos de destino CoordenacaoCurso (docs/permissoes.md): as demais opções nunca casariam.
  const especialidadesVisiveis = usuario?.perfil === "CoordenadorCurso" ? ESPECIALIDADES.filter((x) => x === "CoordenacaoCurso") : ESPECIALIDADES;
  return (
    <div role="search" aria-label="Filtrar alunos" className="flex flex-wrap gap-2">
      <BuscaUrl id={`${id}-busca`} rotulo="Buscar por nome ou matrícula" valor={buscaUrl} aoMudar={(v) => atualizar("busca", v ?? "")} maxLength={200} className="min-w-[220px] flex-1" />
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
      <label htmlFor={`${id}-profissional`} className="sr-only">
        Profissional responsável
      </label>
      <Select
        id={`${id}-profissional`}
        value={params.get("profissionalId") ?? ""}
        onChange={(e) => atualizar("profissionalId", e.target.value)}
        className={classeFiltro}
      >
        <option value="">Profissional: todos</option>
        {profissionais.data?.map((p) => (
          <option key={p.id} value={p.id}>
            {p.nome}
          </option>
        ))}
      </Select>
      <label htmlFor={`${id}-situacao`} className="sr-only">
        Situação
      </label>
      <Select id={`${id}-situacao`} value={params.get("situacao") ?? ""} onChange={(e) => atualizar("situacao", e.target.value)} className={classeFiltro}>
        <option value="">Situação: todas</option>
        {SITUACOES.map((s) => (
          <option key={s} value={s}>
            {ROTULO_SITUACAO[s]}
          </option>
        ))}
      </Select>
      <label htmlFor={`${id}-encaminhamento`} className="sr-only">
        Tipo de encaminhamento
      </label>
      <Select
        id={`${id}-encaminhamento`}
        value={params.get("especialidadeEncaminhamento") ?? ""}
        onChange={(e) => atualizar("especialidadeEncaminhamento", e.target.value)}
        className={classeFiltro}
      >
        <option value="">Encaminhamento: todos</option>
        {especialidadesVisiveis.map((x) => (
          <option key={x} value={x}>
            {ROTULO_ESPECIALIDADE[x]}
          </option>
        ))}
      </Select>
      <div className="flex w-full min-w-0 flex-wrap items-center gap-1.5 text-denso text-suave sm:w-auto">
        <span className="flex flex-1 items-center gap-1.5">
          <label htmlFor={`${id}-de`} className="whitespace-nowrap">Início de</label>
          <Input id={`${id}-de`} type="date" value={params.get("inicioDe") ?? ""} onChange={(e) => atualizar("inicioDe", e.target.value)} className={classeData} />
        </span>
        <span className="flex flex-1 items-center gap-1.5">
          <label htmlFor={`${id}-ate`}>até</label>
          <Input id={`${id}-ate`} type="date" value={params.get("inicioAte") ?? ""} onChange={(e) => atualizar("inicioAte", e.target.value)} className={classeData} />
        </span>
      </div>
    </div>
  );
}
