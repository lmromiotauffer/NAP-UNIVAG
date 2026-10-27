import { useState, type ReactNode } from "react";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { Dado as DadoBase } from "../../../components/ui/Dado";
import { ErrorState } from "../../../components/ui/ErrorState";
import { Modal } from "../../../components/ui/Modal";
import { Carregando } from "../../../components/ui/Carregando";
import { Skeleton } from "../../../components/ui/Skeleton";
import { useToast } from "../../../components/ui/Toast";
import { cn } from "../../../lib/cn";
import { formatarData, formatarDataHora, formatarDuracao, formatarIntervalo } from "../../../lib/format";
import { useDetalheAtividade } from "../hooks/useAtividades";
import { ROTULO_TIPO_ATIVIDADE, ROTULO_TURNO } from "../rotulos";
import { CorrigirAtividadeDialog } from "./CorrigirAtividadeDialog";

const Dado = (p: { rotulo: string; children: ReactNode }) => <DadoBase classeValor="break-words whitespace-pre-line" {...p} />;

/** Detalhe de uma atividade; "Corrigir" só aparece quando a API diz que o usuário pode (`podeCorrigir`). */
export function DetalheAtividade({ id, aoFechar }: { id: string; aoFechar: () => void }) {
  const toast = useToast();
  const detalhe = useDetalheAtividade(id);
  const [corrigindo, setCorrigindo] = useState(false);
  const [voltouDaCorrecao, setVoltouDaCorrecao] = useState(false);
  const fecharCorrecao = () => {
    setCorrigindo(false);
    setVoltouDaCorrecao(true);
  };
  const a = detalhe.data;

  let corpo;
  if (detalhe.isPending) {
    corpo = (
      <Carregando rotulo="Carregando atividade" className="flex flex-col gap-3">
        <Skeleton className="h-5 w-40" />
        <Skeleton className="h-16 w-full" />
        <Skeleton className="h-24 w-full" />
      </Carregando>
    );
  } else if (detalhe.isError && !detalhe.data) {
    corpo = <ErrorState erro={detalhe.error} aoTentarNovamente={() => void detalhe.refetch()} />;
  } else if (corrigindo) {
    corpo = (
      <CorrigirAtividadeDialog
        atividade={detalhe.data}
        aoCancelar={fecharCorrecao}
        aoConcluir={() => {
          toast.sucesso("Atividade corrigida.");
          fecharCorrecao();
        }}
      />
    );
  } else {
    const d = detalhe.data;
    corpo = (
      // Surge só ao voltar do formulário (mesma troca de painel que o formulário faz); ao substituir o skeleton, apagaria e surgiria de novo.
      <div className={cn("flex flex-col gap-4 text-sm", voltouDaCorrecao && "animate-surgir")}>
        <div className="flex flex-wrap items-center gap-2">
          <Badge variante="atividade">{ROTULO_TIPO_ATIVIDADE[d.tipo]}</Badge>
          {d.avulsa && <Badge>Avulsa</Badge>}
        </div>
        <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Dado rotulo="Horário">
            {formatarData(d.inicio)}, {formatarIntervalo(d.inicio, d.fim)} · {formatarDuracao(d.duracaoMinutos)} · {ROTULO_TURNO[d.turno]}
          </Dado>
          <Dado rotulo="Profissional">{d.profissional}</Dado>
          <Dado rotulo="Curso">{d.curso ?? "Institucional"}</Dado>
          {d.avulsa && <Dado rotulo="Justificativa da atividade avulsa">{d.justificativaAvulso ?? "—"}</Dado>}
        </dl>
        <dl>
          <Dado rotulo="Descrição">{d.descricao}</Dado>
        </dl>
        {d.correcoes.length > 0 && (
          <section aria-labelledby="titulo-correcoes" className="flex flex-col gap-2">
            <h3 id="titulo-correcoes" className="m-0 text-xs font-semibold tracking-wide text-suave uppercase">
              Correções ({d.correcoes.length})
            </h3>
            <ol className="m-0 flex list-none flex-col gap-2 p-0">
              {d.correcoes.map((c) => (
                <li key={c.em} className="rounded-md border border-borda px-3 py-2 break-words">
                  <div className="font-medium">
                    {c.autor} · {formatarDataHora(c.em)}
                  </div>
                  <div className="text-suave">
                    Antes: {ROTULO_TIPO_ATIVIDADE[c.tipoAnterior]} · {c.cursoAnterior ?? "Institucional"} · {formatarData(c.inicioAnterior)}, {formatarIntervalo(c.inicioAnterior, c.fimAnterior)} ·{" "}
                    {c.descricaoAnterior}
                  </div>
                  <div>Justificativa: {c.justificativa}</div>
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
      titulo={corrigindo ? "Corrigir atividade" : "Atividade"}
      rodape={!corrigindo && a?.podeCorrigir ? <Button variante="secundaria" onClick={() => setCorrigindo(true)}>Corrigir</Button> : undefined}
    >
      {corpo}
    </Modal>
  );
}
