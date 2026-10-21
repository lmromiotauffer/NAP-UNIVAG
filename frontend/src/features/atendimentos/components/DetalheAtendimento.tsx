import { Lock } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Link } from "react-router";
import { ehCoordenacao, pode } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { Carregando } from "../../../components/ui/Carregando";
import { Dado as DadoBase } from "../../../components/ui/Dado";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Modal } from "../../../components/ui/Modal";
import { Skeleton } from "../../../components/ui/Skeleton";
import { useToast } from "../../../components/ui/Toast";
import { formatarData, formatarDataHora, formatarDuracao, formatarHora } from "../../../lib/format";
import { estiloAtendimento, ROTULO_TIPO_ATENDIMENTO } from "../../agenda/components/estiloCompromisso";
import { useDetalheAtendimento } from "../hooks/useAtendimentos";
import { ROTULO_PRIVADAS } from "./AtendimentoForm";
import { CorrigirAtendimentoDialog } from "./CorrigirAtendimentoDialog";

const Dado = (p: { rotulo: string; children: ReactNode }) => <DadoBase classeValor="whitespace-pre-line" {...p} />;

const minutos = (inicio: string, fim: string) => Math.round((Date.parse(fim) - Date.parse(inicio)) / 60_000);

/** Detalhe de um atendimento. A API audita a leitura: só montar quando o usuário abre o atendimento. */
export function DetalheAtendimento({ id, aoFechar }: { id: string; aoFechar: () => void }) {
  const { usuario } = useSessao();
  const toast = useToast();
  const detalhe = useDetalheAtendimento(id);
  const [corrigindo, setCorrigindo] = useState(false);
  const a = detalhe.data;
  const podeCorrigir = !!a && !!usuario && pode(usuario, "atendimentos.corrigir") && (ehCoordenacao(usuario) || a.profissionalId === usuario.id);

  let corpo;
  if (detalhe.isPending) {
    corpo = (
      <Carregando rotulo="Carregando atendimento" className="flex flex-col gap-3">
        <Skeleton className="h-5 w-40" />
        <Skeleton className="h-16 w-full" />
        <Skeleton className="h-24 w-full" />
      </Carregando>
    );
  } else if (detalhe.isError && !detalhe.data) {
    corpo = <ErrorState erro={detalhe.error} aoTentarNovamente={() => void detalhe.refetch()} />;
  } else if (corrigindo && usuario) {
    corpo = (
      <CorrigirAtendimentoDialog
        atendimento={detalhe.data}
        usuarioId={usuario.id}
        aoCancelar={() => setCorrigindo(false)}
        aoConcluir={() => {
          toast.sucesso("Atendimento corrigido.");
          setCorrigindo(false);
        }}
      />
    );
  } else {
    const d = detalhe.data;
    const estilo = estiloAtendimento(d.estado);
    const clinicos = d.dadosClinicos;
    corpo = (
      <div className="flex flex-col gap-4 text-sm">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variante={estilo.variante}>{estilo.rotuloEstado}</Badge>
          <span className="text-suave">{ROTULO_TIPO_ATENDIMENTO[d.tipo]}</span>
          {d.avulso && <Badge>Avulso</Badge>}
        </div>
        <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Dado rotulo="Horário">
            {formatarData(d.inicio)}, {formatarHora(d.inicio)}–{formatarHora(d.fim)} · {formatarDuracao(minutos(d.inicio, d.fim))}
          </Dado>
          <Dado rotulo="Profissional">{d.profissional}</Dado>
          <Dado rotulo="Aluno">
            <Link to={`/alunos/${d.alunoId}`} className="font-semibold text-primaria underline-offset-2 hover:underline">
              {d.aluno}
            </Link>
          </Dado>
          {d.avulso && <Dado rotulo="Justificativa do avulso">{d.justificativaAvulso ?? "—"}</Dado>}
        </dl>
        {d.estado === "Ausente" && !clinicos?.demandaPrincipal ? (
          <p className="m-0 text-suave">O aluno faltou. Ausência não exige demanda ou nota clínica.</p>
        ) : (
          <dl className="flex flex-col gap-3">
            <Dado rotulo="Demanda principal">{clinicos?.demandaPrincipal ?? "—"}</Dado>
            <Dado rotulo="Recomendações e encaminhamentos">{clinicos?.recomendacoesEncaminhamento ?? "—"}</Dado>
            <Dado rotulo="Próximos passos">{clinicos?.proximosPassos ?? "—"}</Dado>
          </dl>
        )}
        {clinicos?.observacoesPrivadas && (
          <section aria-label="Observações privadas" className="rounded-md border border-borda bg-realce px-3 py-2.5">
            <p className="m-0 mb-1 flex items-center gap-1.5 text-xs font-semibold text-suave">
              <Lock aria-hidden="true" className="size-3.5" />
              Observações privadas · {ROTULO_PRIVADAS}
            </p>
            <p className="m-0 whitespace-pre-line">{clinicos.observacoesPrivadas}</p>
          </section>
        )}
        {d.correcoes.length > 0 && (
          <section aria-labelledby="titulo-correcoes" className="flex flex-col gap-2">
            <h3 id="titulo-correcoes" className="m-0 text-xs font-semibold tracking-wide text-suave uppercase">
              Correções ({d.correcoes.length})
            </h3>
            <ol className="m-0 flex list-none flex-col gap-2 p-0">
              {d.correcoes.map((c) => (
                <li key={c.em} className="rounded-md border border-borda px-3 py-2">
                  <div className="font-medium">
                    {c.autor} · {formatarDataHora(c.em)}
                  </div>
                  <div className="text-suave">
                    Antes: {estiloAtendimento(c.estadoAnterior).rotuloEstado} · {ROTULO_TIPO_ATENDIMENTO[c.tipoAnterior]} · {formatarData(c.inicioAnterior)}, {formatarHora(c.inicioAnterior)}–
                    {formatarHora(c.fimAnterior)}. Depois: {estiloAtendimento(c.estadoNovo).rotuloEstado}.
                  </div>
                  {c.justificativa && <div>Justificativa: {c.justificativa}</div>}
                </li>
              ))}
            </ol>
          </section>
        )}
      </div>
    );
  }

  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo={corrigindo ? "Corrigir atendimento" : "Atendimento"}
      rodape={!corrigindo && podeCorrigir ? <Button variante="secundaria" onClick={() => setCorrigindo(true)}>Corrigir</Button> : undefined}
    >
      {corpo}
    </Modal>
  );
}
