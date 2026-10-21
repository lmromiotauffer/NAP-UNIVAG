import { GraduationCap, Plus } from "lucide-react";
import { useState } from "react";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { ConfirmDialog } from "../../../components/ui/ConfirmDialog";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Modal } from "../../../components/ui/Modal";
import { Skeleton } from "../../../components/ui/Skeleton";
import { classeMolduraRolavel, Table, th, td } from "../../../components/ui/Table";
import { useToast } from "../../../components/ui/Toast";
import { mensagemErro } from "../../../lib/api";
import type { Curso } from "../../../lib/types";
import { useCursos } from "../../compartilhado/catalogos";
import { useAlterarSituacaoCurso } from "../hooks/useAdministracao";
import { CursoForm } from "./CursoForm";


export function CursosTab() {
  const toast = useToast();
  const cursos = useCursos();
  const situacao = useAlterarSituacaoCurso();
  // `undefined` = fechado; sem `curso` = novo. O curso do diálogo é o snapshot de quando abriu (versão do If-Match).
  const [editando, setEditando] = useState<{ curso?: Curso } | undefined>();
  const [alterando, setAlterando] = useState<Curso | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  async function confirmar() {
    if (!alterando) return;
    setErro(null);
    try {
      await situacao.mutateAsync({ id: alterando.id, body: { ativo: !alterando.ativo }, versao: alterando.versao });
      toast.sucesso(alterando.ativo ? "Curso inativado." : "Curso reativado.");
      setAlterando(null);
    } catch (e) {
      setErro(mensagemErro(e));
    }
  }

  let corpo;
  if (cursos.isError) {
    corpo = (
      <div className="p-4">
        <ErrorState erro={cursos.error} aoTentarNovamente={() => void cursos.refetch()} />
      </div>
    );
  } else if (cursos.data?.length === 0) {
    corpo = <EmptyState icone={<GraduationCap />} titulo="Nenhum curso cadastrado" descricao="Cadastre os cursos para vincular alunos e coordenadores." />;
  } else {
    corpo = (
      <Table caption="Cursos">
        <thead>
          <tr className="border-b border-borda">
            <th scope="col" className={th}>Curso</th>
            <th scope="col" className={th}>Situação</th>
            <th scope="col" className={th}>
              <span className="sr-only">Ações</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {cursos.data
            ? cursos.data.map((c) => (
                <tr key={c.id} className="border-b border-borda text-corpo">
                  <td className={td}>
                    <span className="font-semibold">{c.nome}</span>
                  </td>
                  <td className={td}>
                    <Badge variante={c.ativo ? "realizado" : "neutro"}>{c.ativo ? "Ativo" : "Inativo"}</Badge>
                  </td>
                  <td className={`${td} text-right`}>
                    <div className="flex flex-wrap justify-end gap-2">
                      <Button tamanho="sm" variante="secundaria" aria-label={`Renomear ${c.nome}`} onClick={() => setEditando({ curso: c })}>
                        Renomear
                      </Button>
                      <Button
                        tamanho="sm"
                        variante="secundaria"
                        aria-label={`${c.ativo ? "Inativar" : "Reativar"} ${c.nome}`}
                        onClick={() => {
                          setErro(null);
                          setAlterando(c);
                        }}
                      >
                        {c.ativo ? "Inativar" : "Reativar"}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))
            : Array.from({ length: 4 }, (_, i) => (
                <tr key={i} aria-hidden="true" className="border-b border-borda">
                  <td className={td}>
                    <Skeleton className="h-4 w-48" />
                  </td>
                  <td className={td}>
                    <Skeleton className="h-5 w-16 rounded-full" />
                  </td>
                  <td className={td} />
                </tr>
              ))}
        </tbody>
      </Table>
    );
  }

  return (
    <>
      <div className="flex justify-end">
        <Button onClick={() => setEditando({})} className="rounded-full">
          <Plus aria-hidden="true" className="size-4" />
          Novo curso
        </Button>
      </div>
      <section aria-label="Lista de cursos" className={classeMolduraRolavel}>
        {corpo}
      </section>
      <Modal aberto={!!editando} aoFechar={() => setEditando(undefined)} titulo={editando?.curso ? "Renomear curso" : "Novo curso"}>
        {editando && (
          <CursoForm
            curso={editando.curso}
            aoCancelar={() => setEditando(undefined)}
            aoConcluir={() => {
              toast.sucesso(editando.curso ? "Curso renomeado." : "Curso cadastrado.");
              setEditando(undefined);
            }}
          />
        )}
      </Modal>
      <ConfirmDialog
        aberto={!!alterando}
        titulo={alterando?.ativo ? "Inativar curso" : "Reativar curso"}
        mensagem={
          alterando?.ativo
            ? `Inativar "${alterando.nome}"? Coordenadores e alunos atuais continuam vinculados, mas o curso deixa de aceitar novas atribuições.`
            : `Reativar "${alterando?.nome}"? O curso volta a poder receber alunos e coordenadores.`
        }
        confirmar={alterando?.ativo ? "Inativar" : "Reativar"}
        aoConfirmar={() => void confirmar()}
        aoCancelar={() => setAlterando(null)}
        carregando={situacao.isPending}
      >
        {erro && (
          <p role="alert" className="text-sm text-urgente">
            {erro}
          </p>
        )}
      </ConfirmDialog>
    </>
  );
}
