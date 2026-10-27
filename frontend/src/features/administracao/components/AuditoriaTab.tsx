import { Fragment, useId, useState, type FormEvent } from "react";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { ScrollText } from "lucide-react";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { classeData, classeFiltro, Input } from "../../../components/ui/Input";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Select } from "../../../components/ui/Select";
import { Skeleton } from "../../../components/ui/Skeleton";
import { classeMolduraRolavel, Table, th, td } from "../../../components/ui/Table";
import { cn } from "../../../lib/cn";
import { formatarDataHora, hojeCivil, somarDias } from "../../../lib/format";
import { useAuditoria, useListaUsuarios } from "../hooks/useAdministracao";
import type { FiltroAuditoria } from "../services/administracaoService";

type Rascunho = { inicio: string; fim: string; usuarioId: string; acao: string; recurso: string; resultado: string };

/** Sem usuarioId: "sucesso" é operação do sistema; "negado" é tentativa sem sessão (login que falhou). Com id e sem nome, o cadastro não o resolve. */
const nomeDoUsuario = (usuarioId: string | null, usuario: string | null, resultado: string) =>
  usuarioId == null && resultado !== "negado" ? "Sistema" : (usuario ?? "Não identificado");

export function AuditoriaTab() {
  // O servidor exige inicio e fim: abre nos últimos 7 dias (hoje e os 6 anteriores).
  const [rascunho, setRascunho] = useState<Rascunho>(() => {
    const hoje = hojeCivil();
    return { inicio: somarDias(hoje, -6), fim: hoje, usuarioId: "", acao: "", recurso: "", resultado: "" };
  });
  const [aplicado, setAplicado] = useState(rascunho);
  const [pagina, setPagina] = useState(1);
  const usuarios = useListaUsuarios({ tamanho: 100 });

  // O servidor aceita no máximo 366 dias contando o dia final.
  const mensagemPeriodo =
    !rascunho.inicio || !rascunho.fim
      ? "Informe o início e o fim do período."
      : rascunho.fim < rascunho.inicio
        ? "A data final não pode ser anterior à inicial."
        : rascunho.fim > somarDias(rascunho.inicio, 365)
          ? "O período não pode passar de 366 dias."
          : null;
  const idMensagemPeriodo = useId();
  const id = useId();
  const periodoInvalido = mensagemPeriodo !== null;
  const filtro: FiltroAuditoria = {
    inicio: aplicado.inicio,
    fim: aplicado.fim,
    usuarioId: aplicado.usuarioId || undefined,
    acao: aplicado.acao.trim() || undefined,
    recurso: aplicado.recurso.trim() || undefined,
    resultado: aplicado.resultado || undefined,
    pagina,
  };
  const lista = useAuditoria(filtro);

  const campo = (chave: keyof Rascunho) => ({ value: rascunho[chave], onChange: (e: { target: { value: string } }) => setRascunho({ ...rascunho, [chave]: e.target.value }) });
  // O período é obrigatório: "filtro" é só o que restringe além dele.
  const comFiltro = !!(aplicado.usuarioId || aplicado.acao.trim() || aplicado.recurso.trim() || aplicado.resultado);

  function filtrar(e: FormEvent) {
    e.preventDefault();
    if (periodoInvalido) return;
    setAplicado(rascunho);
    setPagina(1);
  }

  function limparFiltros() {
    const limpo = { ...aplicado, usuarioId: "", acao: "", recurso: "", resultado: "" };
    setRascunho(limpo);
    setAplicado(limpo);
    setPagina(1);
  }

  let corpo;
  if (lista.isError && !lista.data) {
    corpo = (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  } else if (lista.data && lista.data.total === 0 && !lista.isPlaceholderData) {
    corpo = (
      <EmptyState
        icone={<ScrollText />}
        titulo="Nenhum evento no período"
        descricao="Revise o período ou os filtros selecionados."
        acao={
          comFiltro && (
            <Button variante="secundaria" tamanho="sm" onClick={limparFiltros}>
              Limpar filtros
            </Button>
          )
        }
      />
    );
  } else {
    corpo = (
      <>
        <Table caption="Trilha de auditoria" aria-busy={lista.isPending || lista.isPlaceholderData} className={cn(lista.isPlaceholderData && "opacity-60")}>
          <thead>
            <tr className="border-b border-borda">
              <th scope="col" className={th}>Data e hora</th>
              <th scope="col" className={th}>Usuário</th>
              <th scope="col" className={th}>Ação</th>
              <th scope="col" className={th}>Recurso</th>
              <th scope="col" className={th}>Resultado</th>
            </tr>
          </thead>
          {lista.data ? (
            <tbody>
              {lista.data.itens.map((ev) => (
                <tr key={ev.id} className="border-b border-borda text-corpo">
                  <td className={cn(td, "whitespace-nowrap tabular-nums")}>{formatarDataHora(ev.em)}</td>
                  {/* No celular a tabela já rola na horizontal: usuário e ação ficam numa linha em vez de dobrar a altura. */}
                  <td className={cn(td, "max-sm:whitespace-nowrap")}>{nomeDoUsuario(ev.usuarioId, ev.usuario, ev.resultado)}</td>
                  {/* Quebra só depois dos pontos ("encaminhamento.<wbr>consulta"), nunca letra a letra. */}
                  <td className={cn(td, "break-words max-sm:whitespace-nowrap max-sm:[&_wbr]:hidden")}>{ev.acao.split(".").map((parte, i) => <Fragment key={i}>{i > 0 && <>.<wbr /></>}{parte}</Fragment>)}</td>
                  <td className={cn(td, "whitespace-nowrap")}>
                    {ev.recurso}
                    {/* Id na mesma linha, cortado com reticências; o valor inteiro fica no DOM (leitor de tela, cópia) e no title. */}
                    {ev.recursoId && (
                      <>
                        {" "}
                        <span title={ev.recursoId} className="inline-block max-w-[10rem] truncate align-bottom text-xs text-suave lg:max-w-[14rem] min-[88rem]:max-w-none">
                          {ev.recursoId}
                        </span>
                      </>
                    )}
                  </td>
                  <td className={td}>{ev.resultado === "negado" ? <Badge variante="urgente">negado</Badge> : ev.resultado}</td>
                </tr>
              ))}
            </tbody>
          ) : (
            <tbody aria-hidden="true">
              {Array.from({ length: 6 }, (_, i) => (
                <tr key={i} className="border-b border-borda">
                  <td className={td} colSpan={5}>
                    <Skeleton className="h-4 w-full" />
                  </td>
                </tr>
              ))}
            </tbody>
          )}
        </Table>
        {lista.data && (
          <div className="px-4 md:mt-auto">
            <Paginacao pagina={lista.data.pagina} tamanho={lista.data.tamanho} total={lista.data.total} aoMudar={setPagina} />
          </div>
        )}
      </>
    );
  }

  return (
    <>
      {/* Mesma barra das outras listagens: período com rótulos na linha; demais rótulos só para leitores de tela (o texto do campo já diz o que é). */}
      <form role="search" aria-label="Filtrar auditoria" onSubmit={filtrar} className="flex flex-wrap items-center gap-2">
        {/* Cada rótulo quebra de linha junto com o seu campo: no celular o "Até" não fica órfão no fim da linha. */}
        <div className="flex w-full min-w-0 flex-wrap items-center gap-1.5 text-denso text-suave sm:w-auto">
          <span className="flex flex-1 items-center gap-1.5">
            <label htmlFor={`${id}-inicio`}>De</label>
            {/* Cada data só fica inválida pelo que lhe cabe: a faixa (ordem, 366 dias) é apontada no "Até". */}
            <Input id={`${id}-inicio`} type="date" required {...campo("inicio")} invalid={!rascunho.inicio} aria-describedby={mensagemPeriodo ? idMensagemPeriodo : undefined} className={classeData} />
          </span>
          <span className="flex flex-1 items-center gap-1.5">
            <label htmlFor={`${id}-fim`}>Até</label>
            <Input
              id={`${id}-fim`}
              type="date"
              required
              {...campo("fim")}
              invalid={!rascunho.fim || (!!rascunho.inicio && periodoInvalido)}
              aria-describedby={mensagemPeriodo ? idMensagemPeriodo : undefined}
              className={classeData}
            />
          </span>
        </div>
        <label htmlFor={`${id}-usuario`} className="sr-only">
          Usuário
        </label>
        <Select id={`${id}-usuario`} {...campo("usuarioId")} className={classeFiltro}>
          <option value="">Todos os usuários</option>
          {usuarios.data?.itens.map((u) => (
            <option key={u.id} value={u.id}>
              {u.nome}
            </option>
          ))}
        </Select>
        <label htmlFor={`${id}-acao`} className="sr-only">
          Ação
        </label>
        <Input id={`${id}-acao`} type="search" autoComplete="off" placeholder="Ação (ex.: login)" maxLength={100} {...campo("acao")} className={cn(classeFiltro, "w-44")} />
        <label htmlFor={`${id}-recurso`} className="sr-only">
          Recurso
        </label>
        <Input id={`${id}-recurso`} type="search" autoComplete="off" placeholder="Recurso (ex.: Aluno)" maxLength={100} {...campo("recurso")} className={cn(classeFiltro, "w-44")} />
        <label htmlFor={`${id}-resultado`} className="sr-only">
          Resultado
        </label>
        <Select id={`${id}-resultado`} {...campo("resultado")} className={classeFiltro}>
          <option value="">Resultado: todos</option>
          <option value="sucesso">Sucesso</option>
          <option value="negado">Negado</option>
        </Select>
        <Button type="submit" disabled={periodoInvalido} className="rounded-full">
          Filtrar
        </Button>
      </form>
      {mensagemPeriodo && (
        <p id={idMensagemPeriodo} role="alert" className="m-0 animate-surgir text-sm text-urgente">
          {mensagemPeriodo}
        </p>
      )}
      <section aria-label="Eventos de auditoria" className={classeMolduraRolavel}>
        {corpo}
      </section>
      {lista.isError && lista.data && <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} className="animate-surgir" />}
    </>
  );
}
