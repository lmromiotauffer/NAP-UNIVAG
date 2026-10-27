import { Plus, Presentation } from "lucide-react";
import { useId, useState } from "react";
import { useSearchParams } from "react-router";
import { ehCoordenacao, pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { Button } from "../../components/ui/Button";
import { classeIconeTitulo, classeTituloPagina } from "../../components/ui/Card";
import { classeData, classeFiltro, Input } from "../../components/ui/Input";
import { Modal } from "../../components/ui/Modal";
import { Select } from "../../components/ui/Select";
import { classeMolduraRolavel } from "../../components/ui/Table";
import { useToast } from "../../components/ui/Toast";
import { cn } from "../../lib/cn";
import { diaValido } from "../../lib/format";
import { comParametro, paginaDaUrl } from "../../lib/queryKeys";
import { useCursos, useProfissionais } from "../compartilhado/catalogos";
import { AtividadeForm } from "./components/AtividadeForm";
import { entre } from "../../lib/entre";
import { ROTULO_TIPO_ATIVIDADE, TIPOS_ATIVIDADE } from "./rotulos";
import { DetalheAtividade } from "./components/DetalheAtividade";
import { ListaAtividades } from "./components/ListaAtividades";
import type { FiltroAtividades } from "./services/atividadeService";

const CHAVES_FILTRO = ["inicio", "fim", "cursoId", "profissionalId", "tipo", "institucionais"] as const;

/** A URL é a fonte da verdade dos filtros (recarregar, voltar e compartilhar o link mantêm a lista). */
function filtroDaUrl(params: URLSearchParams): FiltroAtividades {
  const f: FiltroAtividades = {};
  const texto = (c: string) => params.get(c)?.trim() || undefined;
  const inicio = texto("inicio");
  // diaValido, não só a forma: "2026-13-99" num link antigo iria para a API e viraria 400.
  if (inicio && diaValido(inicio)) f.inicio = inicio;
  const fim = texto("fim");
  if (fim && diaValido(fim)) f.fim = fim;
  f.cursoId = texto("cursoId");
  f.profissionalId = texto("profissionalId");
  const tipo = entre(TIPOS_ATIVIDADE, texto("tipo"));
  if (tipo) f.tipo = tipo;
  if (params.get("institucionais") === "true") f.institucionais = true;
  f.pagina = paginaDaUrl(params);
  return f;
}

export function AtividadesPage() {
  const { usuario } = useSessao();
  const [params, setParams] = useSearchParams();
  const toast = useToast();
  const id = useId();
  const podeRegistrar = pode(usuario, "atividades.registrar");
  // A coordenação de curso só enxerga as atividades do próprio curso: o filtro de curso não faria sentido.
  const porCurso = usuario?.perfil !== "CoordenadorCurso";
  const profissionais = useProfissionais();
  const cursos = useCursos();
  const [registrando, setRegistrando] = useState(false);
  const filtro = filtroDaUrl(params);
  const comFiltro = CHAVES_FILTRO.some((c) => params.get(c)?.trim());
  const aberto = params.get("id");

  const alterar = (chave: (typeof CHAVES_FILTRO)[number] | "pagina" | "id", valor: string | null) =>
    setParams((atual) => comParametro(atual, chave, valor), { replace: chave !== "id" });

  return (
    <>
      <title>Atividades nos cursos · NAP</title>
      <header className="flex flex-wrap items-center justify-between gap-3">
        {/* flex-1: em 390 px "Atividades nos cursos" quebra dentro do título em vez de empurrar o botão para outra linha. */}
        <h1 className={cn(classeTituloPagina, "flex flex-1 items-center gap-3")}>
          <span aria-hidden="true" className={classeIconeTitulo}>
            <Presentation className="size-5" />
          </span>
          Atividades nos cursos
        </h1>
        {podeRegistrar && (
          // No celular só o ícone, na linha do título; o texto continua como nome acessível.
          <Button className="rounded-full max-sm:px-3" title="Nova atividade" onClick={() => setRegistrando(true)}>
            <Plus aria-hidden="true" className="size-4" />
            <span className="max-sm:sr-only">Nova atividade</span>
          </Button>
        )}
      </header>

      <div role="search" aria-label="Filtrar atividades" className="flex flex-wrap items-center gap-2">
        <div className="flex w-full min-w-0 flex-wrap items-center gap-1.5 text-denso text-suave sm:w-auto">
          <span className="flex flex-1 items-center gap-1.5">
            <label htmlFor={`${id}-inicio`}>De</label>
            <Input id={`${id}-inicio`} type="date" value={filtro.inicio ?? ""} onChange={(e) => alterar("inicio", e.target.value)} className={classeData} />
          </span>
          <span className="flex flex-1 items-center gap-1.5">
            <label htmlFor={`${id}-fim`}>até</label>
            <Input id={`${id}-fim`} type="date" value={filtro.fim ?? ""} onChange={(e) => alterar("fim", e.target.value)} className={classeData} />
          </span>
        </div>
        {porCurso && (
          <>
            <label htmlFor={`${id}-curso`} className="sr-only">
              Curso
            </label>
            <Select id={`${id}-curso`} value={filtro.cursoId ?? ""} onChange={(e) => alterar("cursoId", e.target.value)} className={classeFiltro}>
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
        <label htmlFor={`${id}-tipo`} className="sr-only">
          Tipo
        </label>
        <Select id={`${id}-tipo`} value={filtro.tipo ?? ""} onChange={(e) => alterar("tipo", e.target.value)} className={classeFiltro}>
          <option value="">Tipo: todos</option>
          {TIPOS_ATIVIDADE.map((t) => (
            <option key={t} value={t}>
              {ROTULO_TIPO_ATIVIDADE[t]}
            </option>
          ))}
        </Select>
        {porCurso && (
          <label className="flex min-h-[2.375rem] cursor-pointer items-center gap-2 text-denso pointer-coarse:min-h-11">
            <input type="checkbox" className="size-4 accent-primaria" checked={!!filtro.institucionais} onChange={(e) => alterar("institucionais", e.target.checked ? "true" : null)} />
            Só institucionais
          </label>
        )}
      </div>

      <section aria-label="Lista de atividades" className={classeMolduraRolavel}>
        <ListaAtividades
          filtro={filtro}
          comFiltro={comFiltro}
          aoMudarPagina={(p) => alterar("pagina", p > 1 ? String(p) : null)}
          aoAbrir={(a) => alterar("id", a)}
          vazio={{
            titulo: "Nenhuma atividade registrada",
            descricao: podeRegistrar ? "Registre atividades a partir da agenda ou como avulsas." : "As atividades do seu curso aparecem aqui.",
            acao: podeRegistrar ? (
              <Button tamanho="sm" onClick={() => setRegistrando(true)}>
                Nova atividade
              </Button>
            ) : undefined,
            acaoFiltro: (
              <Button variante="secundaria" tamanho="sm" onClick={() => setParams({})}>
                Limpar filtros
              </Button>
            ),
          }}
        />
        <p className="m-0 px-5 pb-3 text-rotulo text-suave">Correções são justificadas e auditadas. Atividade sem curso é institucional.</p>
      </section>

      {podeRegistrar && (
        <Modal aberto={registrando} aoFechar={() => setRegistrando(false)} titulo="Nova atividade avulsa">
          <AtividadeForm
            coordenacao={ehCoordenacao(usuario)}
            aoCancelar={() => setRegistrando(false)}
            aoConcluir={() => {
              toast.sucesso("Atividade registrada.");
              setRegistrando(false);
            }}
          />
        </Modal>
      )}
      {aberto && <DetalheAtividade key={aberto} id={aberto} aoFechar={() => alterar("id", null)} />}
    </>
  );
}
