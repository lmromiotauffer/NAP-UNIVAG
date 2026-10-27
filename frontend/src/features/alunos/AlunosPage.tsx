import { ChevronRight, Plus, Users } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { classeIconeTitulo, classeTituloPagina } from "../../components/ui/Card";
import { EmptyState } from "../../components/ui/EmptyState";
import { ErrorState } from "../../components/ui/ErrorState";
import { Modal } from "../../components/ui/Modal";
import { Paginacao } from "../../components/ui/Paginacao";
import { Skeleton } from "../../components/ui/Skeleton";
import { Table, classeAbrirLinha, classeMolduraRolavel, th, td, soLargo, soMedio, trClicavel } from "../../components/ui/Table";
import { useToast } from "../../components/ui/Toast";
import { cn } from "../../lib/cn";
import { formatarInteiro } from "../../lib/format";
import { comParametro, temFiltroAtivo } from "../../lib/queryKeys";
import { AlunoForm } from "./components/AlunoForm";
import { ROTULO_SITUACAO, VARIANTE_SITUACAO } from "./rotulos";
import { CHAVES_FILTRO, FiltrosAlunos, filtroDaUrl } from "./components/FiltrosAlunos";
import { useListaAlunos } from "./hooks/useAlunos";

// No celular a linha fica compacta (padrão de lista): curso, faltas e situação descem para a célula do nome.

function LinhasCarregando() {
  return (
    <tbody aria-hidden="true">
      {Array.from({ length: 8 }, (_, i) => (
        <tr key={i} className="border-b border-borda">
          <td className={td}>
            <Skeleton className="h-4 w-40" />
            <Skeleton className="mt-1.5 h-3 w-20 sm:hidden" />
          </td>
          <td className={cn(td, soLargo)}>
            <Skeleton className="h-4 w-20" />
          </td>
          <td className={cn(td, soLargo)}>
            <Skeleton className="h-4 w-24" />
          </td>
          <td className={cn(td, soLargo)}>
            <Skeleton className="h-4 w-28" />
          </td>
          <td className={cn(td, soMedio)}>
            <Skeleton className="h-5 w-24 rounded-full" />
          </td>
          <td className={cn(td, soMedio)}>
            <Skeleton className="ml-auto h-4 w-6" />
          </td>
          <td className={cn(td, soMedio)}>
            <Skeleton className="ml-auto h-4 w-6" />
          </td>
          <td className={td} />
        </tr>
      ))}
    </tbody>
  );
}

export function AlunosPage() {
  const { usuario } = useSessao();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const toast = useToast();
  const [cadastrando, setCadastrando] = useState(false);
  const filtro = filtroDaUrl(params);
  const comFiltro = temFiltroAtivo(params, CHAVES_FILTRO);
  const lista = useListaAlunos(filtro);
  const podeGerenciar = pode(usuario, "alunos.gerenciar");

  function irParaPagina(pagina: number) {
    setParams((atual) => comParametro(atual, "pagina", pagina > 1 ? String(pagina) : null));
  }

  // Durante a troca de filtro os dados ainda são da consulta anterior: não mostra um total que não corresponde.
  const total = lista.isPlaceholderData ? undefined : lista.data?.total;
  const resumo = total === undefined ? null : `${formatarInteiro(total)} ${total === 1 ? "aluno" : "alunos"}${comFiltro ? " com os filtros atuais" : ""}`;

  let corpo;
  if (lista.isError && !lista.data) {
    corpo = (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  } else if (lista.data && lista.data.total === 0 && !lista.isPlaceholderData) {
    corpo = comFiltro ? (
      <EmptyState
        icone={<Users />}
        titulo="Nenhum aluno corresponde aos filtros"
        descricao="Revise a busca ou os filtros selecionados."
        acao={
          <Button variante="secundaria" tamanho="sm" onClick={() => setParams({})}>
            Limpar filtros
          </Button>
        }
      />
    ) : (
      <EmptyState
        icone={<Users />}
        titulo="Nenhum aluno cadastrado"
        descricao="Os alunos acompanhados pelo NAP aparecem aqui."
        acao={
          podeGerenciar && (
            <Button tamanho="sm" onClick={() => setCadastrando(true)}>
              Cadastrar aluno
            </Button>
          )
        }
      />
    );
  } else if (lista.data && lista.data.itens.length === 0 && !lista.isPlaceholderData) {
    // `pagina` além da última (link antigo ou registros que saíram do filtro).
    corpo = (
      <EmptyState
        icone={<Users />}
        titulo="Esta página não tem alunos"
        descricao={`A lista tem ${formatarInteiro(lista.data.total)} ${lista.data.total === 1 ? "aluno" : "alunos"}.`}
        acao={
          <Button variante="secundaria" tamanho="sm" onClick={() => irParaPagina(1)}>
            Voltar à primeira página
          </Button>
        }
      />
    );
  } else {
    corpo = (
      <>
        <Table caption="Alunos" aria-busy={lista.isPending || lista.isPlaceholderData} className={cn(lista.isPlaceholderData && "opacity-60")}>
          <thead>
            <tr className="border-b border-borda">
              {/* min-w: a 768 px (trilho visível) as outras colunas espremiam o nome em três linhas. */}
              <th scope="col" className={cn(th, "min-w-48")}>Aluno</th>
              <th scope="col" className={cn(th, soLargo)}>Matrícula</th>
              {/* Curso só a partir de lg: entre 768 e 1023 px a coluna do nome (min-w-48) mais as demais não cabem sem rolagem. */}
              <th scope="col" className={cn(th, soLargo)}>Curso</th>
              <th scope="col" className={cn(th, soLargo)}>Profissional</th>
              <th scope="col" className={cn(th, soMedio)}>Situação</th>
              <th scope="col" className={cn(th, soMedio, "text-right")}>Atend.</th>
              <th scope="col" className={cn(th, soMedio, "text-right")}>Faltas</th>
              <th scope="col" className={th}>
                <span className="sr-only">Ações</span>
              </th>
            </tr>
          </thead>
          {lista.data ? (
            <tbody>
              {lista.data.itens.map((a) => (
                <tr key={a.id} className={trClicavel}>
                  <td className={cn(td, "max-sm:pr-2")}>
                    {/* A partir de `sm` o semestre fica na linha do nome; no celular o selo de situação sobe para ela. */}
                    <div className="flex flex-wrap items-center gap-x-1.5 gap-y-0.5">
                      <span className="font-semibold">{a.nome}</span>
                      {/* Semestre, matrícula e situação nulos: aluno transferido, visto pelo coordenador do curso anterior. */}
                      {a.semestre != null && <span className="text-xs whitespace-nowrap text-suave max-sm:hidden">· {a.semestre}º semestre</span>}
                      {a.situacao && (
                        <Badge variante={VARIANTE_SITUACAO[a.situacao]} className="whitespace-nowrap sm:hidden">
                          {ROTULO_SITUACAO[a.situacao]}
                        </Badge>
                      )}
                    </div>
                    <div className="text-xs text-suave sm:hidden">
                      {a.curso || "—"}
                      {a.semestre != null && ` · ${a.semestre}º semestre`}
                      {!!a.totalFaltas && (
                        <span className="font-semibold text-urgente">
                          {" "}
                          · {formatarInteiro(a.totalFaltas)} {a.totalFaltas === 1 ? "falta" : "faltas"}
                        </span>
                      )}
                    </div>
                  </td>
                  <td className={cn(td, soLargo, "text-suave tabular-nums")}>
                    {a.matricula}
                    {a.matriculaAtiva != null && <span className="text-xs"> ({a.matriculaAtiva ? "ativa" : "inativa"})</span>}
                  </td>
                  <td className={cn(td, soLargo)}>{a.curso || "—"}</td>
                  <td className={cn(td, soLargo)}>{a.profissional || "—"}</td>
                  <td className={cn(td, soMedio)}>
                    {a.situacao ? (
                      <Badge variante={VARIANTE_SITUACAO[a.situacao]} className="whitespace-nowrap">
                        {ROTULO_SITUACAO[a.situacao]}
                      </Badge>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td className={cn(td, soMedio, "text-right tabular-nums")}>{formatarInteiro(a.totalAtendimentos)}</td>
                  <td className={cn(td, soMedio, "text-right tabular-nums", a.totalFaltas && "font-semibold text-urgente")}>{formatarInteiro(a.totalFaltas)}</td>
                  <td className={cn(td, "text-right max-sm:pl-0")}>
                    <Link
                      to={`/alunos/${a.id}`}
                      aria-label={`Abrir ficha de ${a.nome}`}
                      className={classeAbrirLinha}
                    >
                      {/* O ::after estende o link à linha toda: o alvo de toque deixa de ser só a seta. */}
                      <span className="hidden lg:inline">Abrir ficha</span>
                      <ChevronRight aria-hidden="true" className="size-3.5" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          ) : (
            <LinhasCarregando />
          )}
        </Table>
        {lista.data && (
          <div className="px-4 md:mt-auto">
            <Paginacao pagina={filtro.pagina ?? 1} tamanho={lista.data.tamanho} total={lista.data.total} aoMudar={irParaPagina} />
          </div>
        )}
      </>
    );
  }

  return (
    <>
      <title>Alunos · NAP</title>
      <header className="flex flex-wrap items-center justify-between gap-3">
        {/* O total fica ao lado do título, não abaixo: uma linha a menos antes da lista. */}
        <div className="flex min-w-0 flex-wrap items-baseline gap-x-3">
          <h1 className={cn(classeTituloPagina, "flex items-center gap-3")}>
            <span aria-hidden="true" className={classeIconeTitulo}>
              <Users className="size-5" />
            </span>
            Alunos
          </h1>
          <p aria-live="polite" className="min-h-5 text-denso text-suave">
            {resumo}
          </p>
        </div>
        {podeGerenciar && (
          // No celular só o ícone, na linha do título; o texto continua como nome acessível.
          <Button onClick={() => setCadastrando(true)} title="Cadastrar aluno" className="rounded-full max-sm:px-3">
            <Plus aria-hidden="true" className="size-4" />
            <span className="max-sm:sr-only">Cadastrar aluno</span>
          </Button>
        )}
      </header>
      <FiltrosAlunos />
      <section aria-label="Lista de alunos" className={cn(classeMolduraRolavel, "relative")}>
        {corpo}
      </section>
      {lista.isError && lista.data && <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} className="animate-surgir" />}
      <Modal aberto={cadastrando} aoFechar={() => setCadastrando(false)} titulo="Cadastrar aluno">
        <AlunoForm
          aoCancelar={() => setCadastrando(false)}
          aoConcluir={(id) => {
            toast.sucesso("Aluno cadastrado.");
            void navigate(`/alunos/${id}`);
          }}
        />
      </Modal>
    </>
  );
}
