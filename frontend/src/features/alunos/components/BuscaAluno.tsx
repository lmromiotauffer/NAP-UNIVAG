import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { useEffect, useId, useState, type ReactNode } from "react";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Input } from "../../../components/ui/Input";
import { mensagemErro } from "../../../lib/api";
import { queryKeys } from "../../../lib/queryKeys";
import { alunoService } from "../services/alunoService";

export type AlunoEscolhido = { id: string; nome: string; matricula?: string };

/** `compacto`: versão de barra de filtros (rótulo só para leitor de tela, resultados flutuantes). */
export function BuscaAluno({
  aluno,
  aoEscolher,
  erro,
  obrigatorio,
  compacto,
}: {
  aluno: AlunoEscolhido | null;
  aoEscolher: (a: AlunoEscolhido | null) => void;
  erro?: string;
  obrigatorio: boolean;
  compacto?: boolean;
}) {
  const [busca, setBusca] = useState("");
  const [aberto, setAberto] = useState(true);
  const idCampo = useId();
  const [termo, setTermo] = useState("");
  const idStatus = useId();
  useEffect(() => {
    const t = setTimeout(() => setTermo(busca.trim()), 300);
    return () => clearTimeout(t);
  }, [busca]);
  const filtro = { busca: termo, tamanho: 6 };
  const resultados = useQuery({
    queryKey: queryKeys.alunos.lista(filtro),
    queryFn: () => alunoService.listar(filtro),
    enabled: !aluno && termo.length >= 2,
  });

  if (aluno)
    return (
      <div className="flex flex-col gap-1">
        <span className="text-sm font-medium text-texto">Aluno</span>
        <div className="flex items-center justify-between gap-2 rounded-md border border-borda bg-realce px-3 py-2 text-sm">
          <span className="min-w-0 break-words">
            <span className="font-semibold">{aluno.nome}</span>
            {aluno.matricula && <span className="text-suave"> · {aluno.matricula}</span>}
          </span>
          <Button tamanho="sm" variante="fantasma" className="flex-none" onClick={() => aoEscolher(null)}>
            Trocar aluno
          </Button>
        </div>
      </div>
    );

  const itens = resultados.data?.itens ?? [];
  const status = (
    <p id={idStatus} aria-live="polite" className="sr-only">
      {resultados.isFetching ? "Buscando alunos…" : termo.length >= 2 ? `${itens.length} ${itens.length === 1 ? "aluno encontrado" : "alunos encontrados"}` : ""}
    </p>
  );
  let retorno: ReactNode = null;
  if (resultados.isError) retorno = <Alert variante="urgente" className="animate-surgir">{mensagemErro(resultados.error)}</Alert>;
  else if (termo.length >= 2 && resultados.isSuccess && itens.length === 0) retorno = <p className={compacto ? "px-3 py-2 text-xs text-suave" : "text-xs text-suave"}>Nenhum aluno encontrado.</p>;
  const lista = itens.length > 0 && (
        <ul className={compacto ? "flex flex-col divide-y divide-borda" : "flex flex-col divide-y divide-borda rounded-md border border-borda"}>
          {itens.map((a) => (
            <li key={a.id}>
              <button
                type="button"
                onClick={() => aoEscolher({ id: a.id, nome: a.nome, matricula: a.matricula })}
                className="flex w-full cursor-pointer justify-between gap-2 px-3 py-2 text-left text-sm transition-colors duration-150 pointer-coarse:min-h-11 hover:bg-realce focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primaria"
              >
                <span className="min-w-0 font-medium break-words">{a.nome}</span>
                <span className="flex-none text-suave tabular-nums">{a.matricula}</span>
              </button>
            </li>
          ))}
        </ul>
  );

  if (compacto)
    return (
      // Fecha ao sair do campo e da lista (o foco que vai para um resultado não conta como saída).
      <div className="relative" onBlur={(e) => !e.currentTarget.contains(e.relatedTarget) && setAberto(false)}>
        <label htmlFor={idCampo} className="sr-only">
          Filtrar por aluno
        </label>
        <Search aria-hidden="true" className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-suave" />
        <Input
          id={idCampo}
          type="search"
          autoComplete="off"
          aria-describedby={idStatus}
          value={busca}
          onChange={(e) => {
            setBusca(e.target.value);
            setAberto(true);
          }}
          onFocus={() => setAberto(true)}
          // Esc com a lista aberta só fecha a lista (sem o preventDefault, o campo de busca nativo apagaria o texto).
          onKeyDown={(e) => {
            if (e.key !== "Escape" || !aberto) return;
            e.preventDefault();
            setAberto(false);
          }}
          placeholder="Aluno: nome ou matrícula"
          className="min-h-[2.375rem] rounded-campo pl-9"
        />
        {status}
        {aberto && (retorno || lista) && (
          <div className="absolute inset-x-0 top-full z-20 mt-1 overflow-hidden rounded-campo border border-borda bg-superficie shadow-lg">
            {retorno}
            {lista}
          </div>
        )}
      </div>
    );

  return (
    <div className="flex flex-col gap-2">
      <Field rotulo="Aluno" erro={erro} obrigatorio={obrigatorio} dica="Busque por nome ou matrícula (mínimo 2 caracteres).">
        <Input type="search" autoComplete="off" aria-describedby={idStatus} value={busca} onChange={(e) => setBusca(e.target.value)} />
      </Field>
      {status}
      {retorno}
      {lista}
    </div>
  );
}
