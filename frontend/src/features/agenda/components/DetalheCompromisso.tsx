import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { Link } from "react-router";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Badge } from "../../../components/ui/Badge";
import { Button } from "../../../components/ui/Button";
import { ConfirmDialog } from "../../../components/ui/ConfirmDialog";
import { Dado } from "../../../components/ui/Dado";
import { Field } from "../../../components/ui/Field";
import { Modal } from "../../../components/ui/Modal";
import { Textarea } from "../../../components/ui/Textarea";
import { useToast } from "../../../components/ui/Toast";
import { tratarErro } from "../../../lib/api";
import { cn } from "../../../lib/cn";
import { formatarData, formatarHora, formatarIntervalo } from "../../../lib/format";
import type { AtendimentoAvulsoAgenda, AtividadeAvulsaAgenda, Compromisso } from "../../../lib/types";
import { RealizacaoDialog } from "../../producao/components/RealizacaoDialog";
import { useCancelarCompromisso } from "../hooks/useAgenda";
import type { EscopoGrupo } from "../services/agendaService";
import { ROTULO_TIPO_ATIVIDADE } from "../../atividades/rotulos";
import { EditarCompromissoForm } from "./CompromissoForm";
import { estiloAtendimento, estiloCompromisso, ROTULO_TIPO, ROTULO_TIPO_ATENDIMENTO } from "./estiloCompromisso";
import { RemarcarDialog } from "./RemarcarDialog";

const esquemaCancelamento = z.object({
  motivo: z.string().trim().min(3, "Informe o motivo (mínimo 3 caracteres).").max(500, "Use no máximo 500 caracteres."),
});
type EntradaCancelamento = z.input<typeof esquemaCancelamento>;
type SaidaCancelamento = z.output<typeof esquemaCancelamento>;

type Modo = "ver" | "editar" | "remarcar" | "cancelar" | "realizar" | "corrigir";

// Só viram produção por "Registrar realização" (atendimento e atividade têm o próprio registro).
const TIPOS_COM_REALIZACAO: Compromisso["tipo"][] = ["ReuniaoEquipe", "ReuniaoCoordenacaoGestao", "ElaboracaoPei", "TarefaAdministrativa", "EncaminhamentoDevolutiva"];

function horario(inicio: string, fim: string): string {
  return `${formatarData(inicio)}, ${formatarIntervalo(inicio, fim)}`;
}

export function DetalheCompromisso({
  compromisso,
  podeAgir,
  propria = true,
  aoFechar,
  aoRemarcado,
  aoRegistrarAtendimento,
  aoRegistrarAtividade,
}: {
  compromisso: Compromisso;
  /** Coordenação ou o próprio profissional (a API recusa os demais). */
  podeAgir: boolean;
  /** A instância é do usuário atual (a coordenação também abre as de outros): define o texto do cancelamento no grupo. */
  propria?: boolean;
  aoFechar: () => void;
  aoRemarcado: (novoId: string, inicio: string) => void;
  /** Abre o registro de atendimento. Sem ele, a ação não aparece. */
  aoRegistrarAtendimento?: (c: Compromisso) => void;
  /** Abre o registro de atividade. Sem ele, a ação não aparece. */
  aoRegistrarAtividade?: (c: Compromisso) => void;
}) {
  const toast = useToast();
  const [modo, setModo] = useState<Modo>("ver");
  const [escopoCancelar, setEscopoCancelar] = useState<EscopoGrupo>("Minha");
  const grupo = !!compromisso.grupoId;
  // Criador ou coordenação (calculado pela API) editam o grupo todo; o participante só cancela a própria instância.
  const podeEditarGrupo = compromisso.podeGerirGrupo;
  const rotuloMinha = propria
    ? Date.parse(compromisso.fim) <= Date.now()
      ? "Não participei (cancelar minha participação)"
      : "Cancelar minha participação"
    : `Cancelar só a participação de ${compromisso.profissional}`;
  const escopo = grupo ? "Grupo" : undefined;
  const cancelarCom = (e: EscopoGrupo) => {
    setEscopoCancelar(e);
    setModo("cancelar");
  };
  const estilo = estiloCompromisso(compromisso);
  const conteudo = useRef<HTMLDivElement>(null);
  const modoAnterior = useRef(modo);

  // Voltando de editar/remarcar, o botão que tinha o foco sumiu: devolve-o ao diálogo.
  useEffect(() => {
    if (modo === "ver" && modoAnterior.current !== "ver") conteudo.current?.closest("dialog")?.querySelector<HTMLElement>("button")?.focus();
    modoAnterior.current = modo;
  }, [modo]);
  const agendado = compromisso.estado === "Agendado";
  const aguardaRealizacao = TIPOS_COM_REALIZACAO.includes(compromisso.tipo) && Date.parse(compromisso.fim) <= Date.now();
  // Concluído desses tipos = realização registrada (só se concluem por ela); o horário exibido é o efetivo.
  const realizado = compromisso.estado === "Concluido" && TIPOS_COM_REALIZACAO.includes(compromisso.tipo);

  if (modo === "cancelar")
    return (
      <CancelarCompromisso
        compromisso={compromisso}
        naoRealizado={aguardaRealizacao}
        grupo={grupo}
        escopo={grupo ? escopoCancelar : undefined}
        rotuloMinha={rotuloMinha}
        propria={propria}
        aoVoltar={() => setModo("ver")}
        aoConcluir={() => {
          toast.sucesso("Compromisso cancelado.");
          aoFechar();
        }}
      />
    );

  const titulo = {
    ver: compromisso.titulo,
    editar: "Editar compromisso",
    remarcar: "Remarcar compromisso",
    realizar: "Registrar realização",
    corrigir: "Corrigir realização",
  }[modo];
  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo={titulo}
      rodape={
        modo === "ver" && agendado && podeAgir ? (
          <>
            {/* Só depois do término: a API recusa registrar atendimento que ainda não terminou. */}
            {aoRegistrarAtendimento && compromisso.tipo === "Atendimento" && Date.parse(compromisso.fim) <= Date.now() && (
              <Button onClick={() => aoRegistrarAtendimento(compromisso)}>Registrar atendimento</Button>
            )}
            {aoRegistrarAtividade && compromisso.tipo === "AtividadeCurso" && Date.parse(compromisso.fim) <= Date.now() && (
              <Button onClick={() => aoRegistrarAtividade(compromisso)}>Registrar atividade</Button>
            )}
            {aguardaRealizacao && <Button onClick={() => setModo("realizar")}>Registrar realização</Button>}
            {!grupo && aguardaRealizacao && (
              <Button variante="secundaria" onClick={() => setModo("cancelar")}>
                Não realizado
              </Button>
            )}
            {!grupo && !aguardaRealizacao && (
              <Button variante="secundaria" onClick={() => setModo("cancelar")}>
                Cancelar compromisso
              </Button>
            )}
            {grupo && (
              <Button variante="secundaria" onClick={() => cancelarCom("Minha")}>
                {rotuloMinha}
              </Button>
            )}
            {grupo && podeEditarGrupo && (
              <Button variante="secundaria" onClick={() => cancelarCom("Grupo")}>
                Cancelar o grupo
              </Button>
            )}
            {(!grupo || podeEditarGrupo) && (
              <Button variante="secundaria" onClick={() => setModo("remarcar")}>
                {grupo ? "Remarcar o grupo" : "Remarcar"}
              </Button>
            )}
            {(!grupo || podeEditarGrupo) && (
              <Button variante="secundaria" onClick={() => setModo("editar")}>
                {grupo ? "Editar o grupo" : "Editar"}
              </Button>
            )}
          </>
        ) : modo === "ver" && realizado && podeAgir ? (
          <Button variante="secundaria" onClick={() => setModo("corrigir")}>
            Corrigir realização
          </Button>
        ) : undefined
      }
    >
      {/* `key`: o painel remonta a cada troca de modo e chega com o surgir padrão. Quando o diálogo monta (abertura e volta do
          cancelamento, que é outro diálogo) quem anima é o próprio diálogo. */}
      <div ref={conteudo} key={modo} className={cn(modoAnterior.current !== modo && modoAnterior.current !== "cancelar" && "animate-surgir")}>
        {modo === "ver" && (
          <div className="flex flex-col gap-4 text-sm">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variante={estilo.variante}>{estilo.rotuloEstado}</Badge>
              <span className="text-suave">{ROTULO_TIPO[compromisso.tipo]}</span>
              {grupo && <Badge variante="info">Grupo</Badge>}
            </div>
            <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Dado rotulo="Horário">{horario(compromisso.inicio, compromisso.fim)}</Dado>
              <Dado rotulo="Profissional">{compromisso.profissional}</Dado>
              {grupo && compromisso.participantes && compromisso.participantes.length > 0 && (
                <Dado rotulo="Participantes">{compromisso.participantes.map((p) => p.nome).join(", ")}</Dado>
              )}
              {compromisso.alunoId && (
                <Dado rotulo="Aluno">
                  <Link to={`/alunos/${compromisso.alunoId}`} className="inline-flex items-center font-semibold text-primaria underline-offset-2 hover:underline pointer-coarse:min-h-11">
                    {compromisso.aluno ?? "Abrir ficha"}
                  </Link>
                </Dado>
              )}
            </dl>
            {/* <dl> próprio, fora da grade de duas colunas: o texto corrido usa a largura toda (como no detalhe do atendimento). */}
            {compromisso.descricao && (
              <dl>
                <Dado rotulo="Descrição" classeValor="whitespace-pre-line">
                  {compromisso.descricao}
                </Dado>
              </dl>
            )}
            {/* A listagem omite os Remarcados: o original nunca está na semana, então o vínculo é só informativo. */}
            {compromisso.remarcadoDeId && <Alert variante="info">Remarcado de um compromisso anterior</Alert>}
            {compromisso.estado === "Concluido" && (
              <p className="text-suave">
                {compromisso.estadoAtendimento
                  ? `Atendimento registrado como ${estilo.rotuloEstado.toLowerCase()}. O horário exibido é o do registro.`
                  : compromisso.tipo === "AtividadeCurso"
                    ? "Atividade registrada. O horário exibido é o do registro."
                    : TIPOS_COM_REALIZACAO.includes(compromisso.tipo)
                      ? "Realização registrada. O horário exibido é o da realização."
                      : "Compromisso concluído."}
              </p>
            )}
          </div>
        )}
        {modo === "editar" && (
          <EditarCompromissoForm
            compromisso={compromisso}
            escopo={escopo}
            aoCancelar={() => setModo("ver")}
            aoConcluir={() => {
              toast.sucesso("Compromisso atualizado.");
              aoFechar();
            }}
          />
        )}
        {modo === "realizar" && (
          <RealizacaoDialog
            compromisso={compromisso}
            aoCancelar={() => setModo("ver")}
            aoConcluir={() => {
              toast.sucesso("Realização registrada.");
              aoFechar();
            }}
          />
        )}
        {modo === "corrigir" && (
          <RealizacaoDialog
            compromisso={compromisso}
            correcao
            aoCancelar={() => setModo("ver")}
            aoConcluir={() => {
              toast.sucesso("Realização corrigida.");
              aoFechar();
            }}
          />
        )}
        {modo === "remarcar" && (
          <RemarcarDialog
            compromisso={compromisso}
            escopo={escopo}
            aoCancelar={() => setModo("ver")}
            aoConcluir={(id, inicio) => {
              toast.sucesso(`Compromisso remarcado para ${formatarData(inicio)}, ${formatarHora(inicio)}.`);
              aoRemarcado(id, inicio);
            }}
          />
        )}
      </div>
    </Modal>
  );
}

function CancelarCompromisso({
  compromisso,
  naoRealizado,
  grupo,
  escopo,
  rotuloMinha,
  propria,
  aoVoltar,
  aoConcluir,
}: {
  compromisso: Compromisso;
  naoRealizado: boolean;
  grupo: boolean;
  escopo?: EscopoGrupo;
  rotuloMinha: string;
  propria: boolean;
  aoVoltar: () => void;
  aoConcluir: () => void;
}) {
  const cancelar = useCancelarCompromisso();
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<EntradaCancelamento, unknown, SaidaCancelamento>({ resolver: zodResolver(esquemaCancelamento), defaultValues: { motivo: "" } });

  // Roda depois do efeito do Modal (filho), que focaria o botão Fechar: o foco fica no motivo.
  useEffect(() => setFocus("motivo"), [setFocus]);

  async function enviar(body: SaidaCancelamento) {
    setErroGeral(null);
    try {
      await cancelar.mutateAsync({ id: compromisso.id, body, versao: compromisso.versao, escopo });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["motivo"] as const, (campo, message) => setError(campo, { message })));
    }
  }

  return (
    <ConfirmDialog
      aberto
      titulo={grupo ? (escopo === "Grupo" ? "Cancelar o grupo" : rotuloMinha) : naoRealizado ? "Não realizado" : "Cancelar compromisso"}
      mensagem={`${compromisso.titulo} · ${horario(compromisso.inicio, compromisso.fim)}. ${
        grupo ? (escopo === "Grupo" ? "O compromisso sai da agenda de todos os participantes" : propria ? "A sua participação sai da agenda e fica registrada como não realizada, se o horário já passou; os demais participantes continuam" : `Só a participação de ${compromisso.profissional} sai da agenda; os demais participantes continuam`) : "O compromisso sai da agenda"
      }; o motivo fica registrado.`}
      confirmar="Confirmar cancelamento"
      cancelar="Voltar"
      carregando={isSubmitting}
      aoCancelar={aoVoltar}
      aoConfirmar={() => void handleSubmit(enviar)()}
    >
      {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
      <Field rotulo="Motivo do cancelamento" erro={errors.motivo?.message} obrigatorio>
        <Textarea {...register("motivo")} />
      </Field>
    </ConfirmDialog>
  );
}

/** Atendimento avulso (registrado sem compromisso): somente leitura. */
export function DetalheAvulso({ atendimento, aoFechar }: { atendimento: AtendimentoAvulsoAgenda; aoFechar: () => void }) {
  const estilo = estiloAtendimento(atendimento.estado);
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Atendimento avulso">
      <div className="flex flex-col gap-4 text-sm">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variante={estilo.variante}>{estilo.rotuloEstado}</Badge>
          <span className="text-suave">{ROTULO_TIPO_ATENDIMENTO[atendimento.tipo]}</span>
        </div>
        <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Dado rotulo="Horário">{horario(atendimento.inicio, atendimento.fim)}</Dado>
          <Dado rotulo="Profissional">{atendimento.profissional}</Dado>
          <Dado rotulo="Aluno">
            <Link to={`/alunos/${atendimento.alunoId}`} className="inline-flex items-center font-semibold text-primaria underline-offset-2 hover:underline pointer-coarse:min-h-11">
              {atendimento.aluno}
            </Link>
          </Dado>
        </dl>
        <p className="text-suave">Registrado sem compromisso na agenda; não pode ser remarcado nem cancelado por aqui.</p>
        <Link to={`/atendimentos?id=${atendimento.id}`} className="inline-flex items-center self-start font-semibold text-primaria underline-offset-2 hover:underline pointer-coarse:min-h-11">
          Ver atendimento
        </Link>
      </div>
    </Modal>
  );
}

export function DetalheAtividadeAvulsa({ atividade, aoFechar }: { atividade: AtividadeAvulsaAgenda; aoFechar: () => void }) {
  return (
    <Modal aberto aoFechar={aoFechar} titulo="Atividade no curso">
      <div className="flex flex-col gap-4 text-sm">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variante="atividade">Registrada</Badge>
          <span className="text-suave">{ROTULO_TIPO_ATIVIDADE[atividade.tipo]}</span>
        </div>
        <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Dado rotulo="Horário">{horario(atividade.inicio, atividade.fim)}</Dado>
          <Dado rotulo="Profissional">{atividade.profissional}</Dado>
        </dl>
        <p className="text-suave">Registrada sem compromisso na agenda; não pode ser remarcada nem cancelada por aqui.</p>
        <Link to={`/atividades?id=${atividade.id}`} className="inline-flex items-center self-start font-semibold text-primaria underline-offset-2 hover:underline pointer-coarse:min-h-11">
          Ver atividade
        </Link>
      </div>
    </Modal>
  );
}
