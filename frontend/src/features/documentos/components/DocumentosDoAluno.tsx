import { zodResolver } from "@hookform/resolvers/zod";
import { FileText } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ehCoordenacao, pode } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Alert } from "../../../components/ui/Alert";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { EmptyState } from "../../../components/ui/EmptyState";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Field } from "../../../components/ui/Field";
import { Modal } from "../../../components/ui/Modal";
import { Paginacao } from "../../../components/ui/Paginacao";
import { Select } from "../../../components/ui/Select";
import { Skeleton } from "../../../components/ui/Skeleton";
import { Table, classeMolduraRolavel, soMedio, th, td } from "../../../components/ui/Table";
import { useToast } from "../../../components/ui/Toast";
import { mensagemErro, tratarErro } from "../../../lib/api";
import { cn } from "../../../lib/cn";
import { formatarData, formatarTamanho } from "../../../lib/format";
import type { Documento } from "../../../lib/types";
import { useAlterarCategoria, useListaDocumentos } from "../hooks/useDocumentos";
import { CATEGORIAS, ROTULO_CATEGORIA, ROTULO_VISIBILIDADE, VARIANTE_VISIBILIDADE } from "../rotulos";
import { documentoService } from "../services/documentoService";
import { AnexarDocumentoDialog } from "./AnexarDocumentoDialog";
import { ArquivarDocumentoDialog } from "./ArquivarDocumentoDialog";
import { CompartilharDocumentoDialog } from "./CompartilharDocumentoDialog";

const esquemaCategoria = z.object({ categoria: z.enum(CATEGORIAS, "Selecione a categoria.") });
type EntradaCategoria = z.input<typeof esquemaCategoria>;

/** Trocar a categoria nunca muda a visibilidade. Montar só enquanto aberto: a versão é a da abertura. */
function AlterarCategoriaDialog({ documento, aoFechar, aoConcluir }: { documento: Documento; aoFechar: () => void; aoConcluir: () => void }) {
  const [versao] = useState(documento.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const alterar = useAlterarCategoria();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<EntradaCategoria>({ resolver: zodResolver(esquemaCategoria), defaultValues: { categoria: documento.categoria } });

  async function enviar(body: EntradaCategoria) {
    setErroGeral(null);
    try {
      await alterar.mutateAsync({ id: documento.id, versao, body });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["categoria"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo="Alterar categoria">
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        <Field rotulo="Categoria" erro={errors.categoria?.message} obrigatorio>
          <Select {...register("categoria")}>
            {CATEGORIAS.map((c) => (
              <option key={c} value={c}>
                {ROTULO_CATEGORIA[c]}
              </option>
            ))}
          </Select>
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" carregando={isSubmitting}>
            Salvar categoria
          </Button>
        </div>
      </form>
    </Modal>
  );
}

type Dialogo = { tipo: "anexar" } | { tipo: "categoria" | "compartilhar" | "revogar" | "arquivar"; documento: Documento } | null;

/** Aba Documentos da ficha. Ler segue `alunos.consultar` (a API recorta por perfil); escrever é só da Equipe NAP. */
export function DocumentosDoAluno({ alunoId }: { alunoId: string }) {
  const { usuario } = useSessao();
  const toast = useToast();
  const gerencia = pode(usuario, "documentos.gerenciar");
  const coordenacao = ehCoordenacao(usuario);
  const [pagina, setPagina] = useState(1);
  const [arquivados, setArquivados] = useState(false);
  const [dialogo, setDialogo] = useState<Dialogo>(null);
  const [baixando, setBaixando] = useState<string | null>(null);
  const lista = useListaDocumentos(alunoId, { pagina, tamanho: 10, ...(coordenacao && arquivados ? { incluirArquivados: true } : {}) });

  async function baixar(d: Documento) {
    setBaixando(d.id);
    try {
      await documentoService.baixar(d);
    } catch (e) {
      toast.erro(mensagemErro(e));
    } finally {
      setBaixando(null);
    }
  }

  const fechar = () => setDialogo(null);
  const concluir = (msg: string) => () => {
    toast.sucesso(msg);
    setDialogo(null);
  };
  const selos = (d: Documento) => (
    <span className="inline-flex flex-wrap gap-1.5">
      <Badge variante={VARIANTE_VISIBILIDADE[d.visibilidade]} className="whitespace-nowrap">
        {ROTULO_VISIBILIDADE[d.visibilidade]}
      </Badge>
      {d.arquivado && <Badge variante="aguardando">Arquivado</Badge>}
    </span>
  );

  let corpo;
  if (lista.isError && !lista.data)
    corpo = (
      <div className="p-4">
        <ErrorState erro={lista.error} aoTentarNovamente={() => void lista.refetch()} />
      </div>
    );
  else if (lista.data && lista.data.total === 0 && !lista.isPlaceholderData)
    corpo = <EmptyState icone={<FileText />} titulo="Nenhum documento" descricao="Os documentos deste aluno aparecem aqui." />;
  else
    corpo = (
      <>
        <Table caption="Documentos" aria-busy={lista.isPending || lista.isPlaceholderData}>
          <thead>
            <tr className="border-b border-borda">
              <th scope="col" className={th}>Documento</th>
              <th scope="col" className={cn(th, soMedio)}>Autor e data</th>
              <th scope="col" className={cn(th, soMedio)}>Visibilidade</th>
              <th scope="col" className={th}>
                <span className="sr-only">Ações</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {lista.data
              ? lista.data.itens.map((d) => (
                  <tr key={d.id} className="border-b border-borda text-corpo">
                    <td className={td}>
                      <div className="font-semibold">
                        {ROTULO_CATEGORIA[d.categoria]} · {d.descricao}
                      </div>
                      <div className="text-rotulo break-all text-suave">
                        {d.nomeArquivo} · {formatarTamanho(d.tamanhoBytes)}
                      </div>
                      {/* No celular a linha fica compacta (como na lista de alunos): autor, data e visibilidade descem para a célula do documento. */}
                      <div className="mt-1 text-rotulo text-suave sm:hidden">
                        {d.autor} · <span className="tabular-nums">{formatarData(d.criadoEm)}</span>
                      </div>
                      <div className="mt-1.5 sm:hidden">{selos(d)}</div>
                    </td>
                    <td className={cn(td, soMedio)}>
                      <div>{d.autor}</div>
                      <div className="text-rotulo text-suave tabular-nums">{formatarData(d.criadoEm)}</div>
                    </td>
                    <td className={cn(td, soMedio)}>{selos(d)}</td>
                    <td className={td}>
                      <div className="flex flex-wrap justify-end gap-1.5">
                        <Button tamanho="sm" variante="secundaria" aria-label={`Baixar ${d.nomeArquivo}`} carregando={baixando === d.id} onClick={() => void baixar(d)}>
                          Baixar
                        </Button>
                        {gerencia && !d.arquivado && (
                          <>
                            <Button tamanho="sm" variante="secundaria" aria-label={`Alterar categoria de ${d.descricao}`} onClick={() => setDialogo({ tipo: "categoria", documento: d })}>
                              Alterar categoria
                            </Button>
                            {d.visibilidade === "CompartilhadoCoordenacao" ? (
                              <Button tamanho="sm" variante="secundaria" aria-label={`Revogar compartilhamento de ${d.descricao}`} onClick={() => setDialogo({ tipo: "revogar", documento: d })}>
                                Revogar compartilhamento
                              </Button>
                            ) : (
                              <Button tamanho="sm" variante="secundaria" aria-label={`Compartilhar ${d.descricao}`} onClick={() => setDialogo({ tipo: "compartilhar", documento: d })}>
                                Compartilhar
                              </Button>
                            )}
                            <Button tamanho="sm" variante="secundaria" aria-label={`Arquivar ${d.descricao}`} onClick={() => setDialogo({ tipo: "arquivar", documento: d })}>
                              Arquivar
                            </Button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              : Array.from({ length: 4 }, (_, i) => (
                  <tr key={i} aria-hidden="true" className="border-b border-borda">
                    {[0, 1, 2, 3].map((j) => (
                      <td key={j} className={cn(td, (j === 1 || j === 2) && soMedio)}>
                        <Skeleton className="h-4 w-full max-w-28" />
                      </td>
                    ))}
                  </tr>
                ))}
          </tbody>
        </Table>
        {lista.data && (
          <div className="px-4 md:mt-auto">
            <Paginacao pagina={lista.data.pagina} tamanho={lista.data.tamanho} total={lista.data.total} aoMudar={setPagina} />
          </div>
        )}
      </>
    );

  return (
    <section aria-labelledby="titulo-documentos" className={classeMolduraRolavel}>
      <div className="flex flex-wrap items-center justify-between gap-2 px-[22px] pt-4 pb-2">
        <h2 id="titulo-documentos" className="m-0 font-titulo text-base font-bold">
          Documentos
        </h2>
        <div className="flex flex-wrap items-center gap-3">
          {coordenacao && (
            <label className="flex cursor-pointer items-center gap-1.5 text-xs pointer-coarse:min-h-11">
              <input
                type="checkbox"
                checked={arquivados}
                onChange={(e) => {
                  setArquivados(e.target.checked);
                  setPagina(1);
                }}
                className="size-4 accent-primaria"
              />
              Mostrar arquivados
            </label>
          )}
          {gerencia && (
            <Button tamanho="sm" onClick={() => setDialogo({ tipo: "anexar" })}>
              Anexar documento
            </Button>
          )}
        </div>
      </div>
      {corpo}
      {dialogo?.tipo === "anexar" && <AnexarDocumentoDialog alunoId={alunoId} aoFechar={fechar} aoConcluir={concluir("Documento anexado.")} />}
      {dialogo?.tipo === "categoria" && <AlterarCategoriaDialog documento={dialogo.documento} aoFechar={fechar} aoConcluir={concluir("Categoria alterada.")} />}
      {(dialogo?.tipo === "compartilhar" || dialogo?.tipo === "revogar") && (
        <CompartilharDocumentoDialog
          documento={dialogo.documento}
          revogar={dialogo.tipo === "revogar"}
          aoFechar={fechar}
          aoConcluir={concluir(dialogo.tipo === "revogar" ? "Compartilhamento revogado." : "Documento compartilhado com a coordenação.")}
        />
      )}
      {dialogo?.tipo === "arquivar" && <ArquivarDocumentoDialog documento={dialogo.documento} aoFechar={fechar} aoConcluir={concluir("Documento arquivado.")} />}
    </section>
  );
}
