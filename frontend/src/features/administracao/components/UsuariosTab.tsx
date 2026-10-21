import { Plus, Users } from "lucide-react";
import { Fragment, useState } from "react";
import { useSearchParams } from "react-router";
import { rotuloPerfil } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Badge } from "../../../components/ui/Badge";
import { BuscaUrl } from "../../../components/ui/BuscaUrl";
import { Button } from "../../../components/ui/Button";
import { ConfirmDialog } from "../../../components/ui/ConfirmDialog";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { classeFiltro } from "../../../components/ui/Input";
import { Modal } from "../../../components/ui/Modal";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Select } from "../../../components/ui/Select";
import { Skeleton } from "../../../components/ui/Skeleton";
import { classeMolduraRolavel, Table, th, td, soMedio } from "../../../components/ui/Table";
import { comParametro, paginaDaUrl } from "../../../lib/queryKeys";
import { useToast } from "../../../components/ui/Toast";
import { cn } from "../../../lib/cn";
import { mensagemErro } from "../../../lib/api";
import type { UsuarioAdmin } from "../../../lib/types";
import { useCursos } from "../../compartilhado/catalogos";
import { useAlterarSituacaoUsuario, useCriarUsuario, useGerarSenhaTemporaria, useListaUsuarios } from "../hooks/useAdministracao";
import type { FiltroUsuariosBody } from "../services/administracaoService";
import { SenhaTemporariaDialog } from "./SenhaTemporariaDialog";
import { PERFIS, UsuarioForm } from "./UsuarioForm";

type Pendente = { acao: "situacao" | "senha"; usuario: UsuarioAdmin };

export function UsuariosTab() {
  const { usuario: eu } = useSessao();
  const toast = useToast();
  const [params, setParams] = useSearchParams();
  const cursos = useCursos();
  const criar = useCriarUsuario();
  const gerar = useGerarSenhaTemporaria();
  const situacao = useAlterarSituacaoUsuario();
  // `undefined` = fechado; sem `usuario` = novo. O usuário do diálogo é o snapshot de quando abriu.
  const [editando, setEditando] = useState<{ usuario?: UsuarioAdmin } | undefined>();
  const [pendente, setPendente] = useState<Pendente | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  // E-mail não é segredo: fica em estado para o diálogo; a senha só existe em `mutation.data`.
  const [emailEntrega, setEmailEntrega] = useState("");

  const ehAdministrador = eu?.perfil === "Administrador";
  const perfil = PERFIS.find((p) => p === params.get("perfil"));
  const cursoId = params.get("cursoId") ?? "";
  const ativo = params.get("ativo") === "true" ? true : params.get("ativo") === "false" ? false : undefined;
  const busca = params.get("busca") ?? "";
  const pagina = paginaDaUrl(params) ?? 1;
  const filtro: FiltroUsuariosBody = { busca: busca || undefined, perfil, cursoId: cursoId || undefined, ativo, pagina };
  const comFiltro = !!(busca || perfil || cursoId || ativo !== undefined);
  const lista = useListaUsuarios(filtro);

  const alterar = (chave: string, valor: string | null) => setParams((atual) => comParametro(atual, chave, valor));

  const senhaEntregue = gerar.data?.senhaTemporaria ?? criar.data?.senhaTemporaria ?? null;
  function fecharSenha() {
    criar.reset();
    gerar.reset();
  }

  async function confirmar() {
    if (!pendente) return;
    setErro(null);
    const { acao, usuario } = pendente;
    try {
      if (acao === "situacao") {
        await situacao.mutateAsync({ id: usuario.id, body: { ativo: !usuario.ativo }, versao: usuario.versao });
        toast.sucesso(usuario.ativo ? "Usuário inativado." : "Usuário reativado.");
      } else {
        setEmailEntrega(usuario.email);
        await gerar.mutateAsync(usuario.id);
      }
      setPendente(null);
    } catch (e) {
      setErro(mensagemErro(e));
    }
  }

  // Coordenação NAP não gerencia administradores; ninguém inativa nem redefine a própria senha por aqui.
  const podeGerenciar = (u: UsuarioAdmin) => ehAdministrador || u.perfil !== "Administrador";
  const ehEu = (u: UsuarioAdmin) => u.id === eu?.id;

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
        titulo="Nenhum usuário corresponde aos filtros"
        descricao="Revise a busca ou os filtros selecionados."
        acao={
          <Button variante="secundaria" tamanho="sm" onClick={() => setParams({})}>
            Limpar filtros
          </Button>
        }
      />
    ) : (
      <EmptyState icone={<Users />} titulo="Nenhum usuário cadastrado" />
    );
  } else {
    corpo = (
      <>
        <Table caption="Usuários" aria-busy={lista.isPending || lista.isPlaceholderData} className={cn(lista.isPlaceholderData && "opacity-60")}>
          <thead>
            <tr className="border-b border-borda">
              <th scope="col" className={th}>Usuário</th>
              <th scope="col" className={cn(th, soMedio)}>Perfil</th>
              <th scope="col" className={cn(th, soMedio)}>Situação</th>
              <th scope="col" className={th}>
                <span className="sr-only">Ações</span>
              </th>
            </tr>
          </thead>
          {lista.data ? (
            <tbody>
              {lista.data.itens.map((u) => (
                <tr key={u.id} className="border-b border-borda text-corpo">
                  <td className={cn(td, "sm:leading-tight")}>
                    {/* No celular a situação sobe para a linha do nome (a coluna Situação some e sobra largura para nome e e-mail). */}
                    <div className="flex flex-wrap items-center gap-1">
                      <span className="mr-0.5 font-semibold">{u.nome}</span>
                      <Badge variante={u.ativo ? "realizado" : "neutro"} className="sm:hidden">{u.ativo ? "Ativo" : "Inativo"}</Badge>
                      {u.deveTrocarSenha && <Badge variante="aguardando" className="sm:hidden">Senha pendente</Badge>}
                    </div>
                    {/* Quebra antes do "@", nunca no meio do nome. */}
                    <div className="text-xs break-words text-suave">{u.email.split("@").map((parte, i) => <Fragment key={i}>{i > 0 && <><wbr />@</>}{parte}</Fragment>)}</div>
                    <div className="text-xs text-suave sm:hidden">
                      {rotuloPerfil(u.perfil)}
                      {u.curso ? ` · ${u.curso}` : ""}
                    </div>
                  </td>
                  <td className={cn(td, soMedio)}>
                    {rotuloPerfil(u.perfil)}
                    {u.curso && <span className="text-xs text-suave"> · {u.curso}</span>}
                  </td>
                  <td className={cn(td, soMedio)}>
                    <div className="flex flex-wrap gap-1">
                      <Badge variante={u.ativo ? "realizado" : "neutro"}>{u.ativo ? "Ativo" : "Inativo"}</Badge>
                      {u.deveTrocarSenha && <Badge variante="aguardando">Senha pendente</Badge>}
                    </div>
                  </td>
                  <td className={td}>
                    {podeGerenciar(u) && (
                      <div className="flex flex-wrap justify-end gap-2 max-sm:min-w-[9rem]">
                        <Button tamanho="sm" variante="secundaria" aria-label={`Editar ${u.nome}`} onClick={() => setEditando({ usuario: u })}>
                          Editar
                        </Button>
                        {!ehEu(u) && (
                          <>
                            <Button
                              tamanho="sm"
                              variante="secundaria"
                              aria-label={`${u.ativo ? "Inativar" : "Reativar"} ${u.nome}`}
                              onClick={() => {
                                setErro(null);
                                setPendente({ acao: "situacao", usuario: u });
                              }}
                            >
                              {u.ativo ? "Inativar" : "Reativar"}
                            </Button>
                            <Button
                              tamanho="sm"
                              variante="secundaria"
                              aria-label={`Gerar senha temporária para ${u.nome}`}
                              onClick={() => {
                                setErro(null);
                                setPendente({ acao: "senha", usuario: u });
                              }}
                            >
                              Gerar senha temporária
                            </Button>
                          </>
                        )}
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          ) : (
            <tbody aria-hidden="true">
              {Array.from({ length: 6 }, (_, i) => (
                <tr key={i} className="border-b border-borda">
                  <td className={td}>
                    <Skeleton className="h-4 w-40" />
                    <Skeleton className="mt-1.5 h-3 w-32" />
                  </td>
                  <td className={cn(td, soMedio)}>
                    <Skeleton className="h-4 w-24" />
                  </td>
                  <td className={cn(td, soMedio)}>
                    <Skeleton className="h-5 w-16 rounded-full" />
                  </td>
                  <td className={td} />
                </tr>
              ))}
            </tbody>
          )}
        </Table>
        {lista.data && (
          <div className="px-4 md:mt-auto">
            <Paginacao pagina={pagina} tamanho={lista.data.tamanho} total={lista.data.total} aoMudar={(p) => alterar("pagina", p > 1 ? String(p) : null)} />
          </div>
        )}
      </>
    );
  }

  const aoAbrirNovo = () => setEditando({});
  const ehSituacao = pendente?.acao === "situacao";
  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <BuscaUrl id="busca-usuario" rotulo="Buscar por nome ou e-mail" valor={busca} aoMudar={(v) => alterar("busca", v)} maxLength={200} className="min-w-0 flex-1 basis-56" />
        <label className="sr-only" htmlFor="filtro-perfil">
          Perfil
        </label>
        <Select id="filtro-perfil" value={perfil ?? ""} onChange={(e) => alterar("perfil", e.target.value || null)} className={classeFiltro}>
          <option value="">Todos os perfis</option>
          {PERFIS.map((p) => (
            <option key={p} value={p}>
              {rotuloPerfil(p)}
            </option>
          ))}
        </Select>
        <label className="sr-only" htmlFor="filtro-curso">
          Curso
        </label>
        <Select id="filtro-curso" value={cursoId} onChange={(e) => alterar("cursoId", e.target.value || null)} className={classeFiltro}>
          <option value="">Todos os cursos</option>
          {cursos.data?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.nome}
            </option>
          ))}
        </Select>
        <label className="sr-only" htmlFor="filtro-ativo">
          Situação
        </label>
        <Select id="filtro-ativo" value={ativo === undefined ? "" : String(ativo)} onChange={(e) => alterar("ativo", e.target.value || null)} className={classeFiltro}>
          <option value="">Todas as situações</option>
          <option value="true">Ativos</option>
          <option value="false">Inativos</option>
        </Select>
        <Button onClick={aoAbrirNovo} className="rounded-full">
          <Plus aria-hidden="true" className="size-4" />
          Novo usuário
        </Button>
      </div>
      <section aria-label="Lista de usuários" className={classeMolduraRolavel}>
        {corpo}
      </section>
      {lista.isError && lista.data && <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />}

      <Modal aberto={!!editando} aoFechar={() => setEditando(undefined)} titulo={editando?.usuario ? "Editar usuário" : "Novo usuário"}>
        {editando && (
          <UsuarioForm
            usuario={editando.usuario}
            ehAdministrador={ehAdministrador}
            ehProprio={!!editando.usuario && ehEu(editando.usuario)}
            aoCriar={(body) => {
              setEmailEntrega(body.email);
              return criar.mutateAsync(body);
            }}
            aoCancelar={() => setEditando(undefined)}
            aoConcluir={() => {
              if (editando.usuario) toast.sucesso("Usuário atualizado.");
              setEditando(undefined);
            }}
          />
        )}
      </Modal>
      <ConfirmDialog
        aberto={!!pendente}
        titulo={ehSituacao ? (pendente.usuario.ativo ? "Inativar usuário" : "Reativar usuário") : "Gerar senha temporária"}
        mensagem={
          !pendente
            ? ""
            : ehSituacao
              ? pendente.usuario.ativo
                ? `Inativar ${pendente.usuario.nome}? Isso encerra imediatamente as sessões abertas desta pessoa.`
                : `Reativar ${pendente.usuario.nome}? A pessoa volta a poder entrar no sistema.`
              : `Gerar uma nova senha temporária para ${pendente.usuario.nome}? A senha atual deixa de valer e as sessões abertas são encerradas.`
        }
        confirmar={ehSituacao ? (pendente.usuario.ativo ? "Inativar" : "Reativar") : "Gerar senha"}
        aoConfirmar={() => void confirmar()}
        aoCancelar={() => setPendente(null)}
        carregando={situacao.isPending || gerar.isPending}
      >
        {erro && (
          <p role="alert" className="text-sm text-urgente">
            {erro}
          </p>
        )}
      </ConfirmDialog>
      <SenhaTemporariaDialog email={emailEntrega} senha={senhaEntregue} aoFechar={fecharSenha} />
    </>
  );
}
