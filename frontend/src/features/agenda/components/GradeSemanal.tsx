import type { CSSProperties, ReactNode } from "react";
import { iniciais } from "../../../components/layout/AppShell";
import { cn } from "../../../lib/cn";
import { diaCivil, diaDaSemana, formatarDataCivil, formatarDiaSemana, formatarHora, hojeCivil, horaCivil } from "../../../lib/format";
import type { AtendimentoAvulsoAgenda, AtividadeAvulsaAgenda, Compromisso, DiaEscala, EscalaSemana } from "../../../lib/types";
import { ROTULO_TIPO_ATIVIDADE } from "../../atividades/rotulos";
import { BlocoCompromisso } from "./BlocoCompromisso";
import { estiloAtendimento, estiloCompromisso, ROTULO_TIPO, ROTULO_TIPO_ATENDIMENTO } from "./estiloCompromisso";

// Mínimo 1 px por minuto (60 px por hora); a grade cresce para ocupar a altura disponível.
const PX_POR_MINUTO = 1;
// 24 px: alvo mínimo de toque (WCAG 2.5.8). Vale em minutos porque a escala nunca fica abaixo de 1 px/min.
const ALTURA_MINIMA = 24;
export const nomeDia = (dia: string): string => diaDaSemana(dia).nome;

/** Segunda a sexta sempre; sábado e domingo quando há faixa na escala ou algum item nessa data (para nada sumir). */
export const diasVisiveis = (escala: EscalaSemana, datasComItem: ReadonlySet<string> = new Set()): DiaEscala[] =>
  escala.dias.filter((d, i) => i < 5 || d.inicio != null || datasComItem.has(d.data));

/** Dias civis em que começa algum item da semana. */
export const datasDosItens = (itens: { inicio: string }[]): Set<string> => new Set(itens.map((i) => diaCivil(i.inicio)));

/** "07:00" ou "07:00:00" → minutos desde a meia-noite. */
function minutos(hora: string): number {
  const [h = 0, m = 0] = hora.split(":").map(Number);
  return h * 60 + m;
}

type Pessoa = { id: string; nome: string };

type Item = { id: string; inicio: string; fim: string; profissionalId: string; profissional: string; lembrete: boolean; render: (posicao: CSSProperties) => ReactNode };

export function GradeSemanal({
  escala,
  compromissos,
  avulsos,
  atividades = [],
  agora,
  equipe,
  diaMovel = 0,
  aoAbrirCompromisso,
  aoAbrirAvulso,
  aoAbrirAtividade,
}: {
  escala: EscalaSemana;
  compromissos: Compromisso[];
  avulsos: AtendimentoAvulsoAgenda[];
  atividades?: AtividadeAvulsaAgenda[];
  agora: Date;
  /** "Toda a equipe": uma sub-coluna por profissional, nesta ordem. Ausente = agenda de um profissional. */
  equipe?: Pessoa[];
  /** Coluna exibida em telas estreitas (índice da faixa). */
  diaMovel?: number;
  aoAbrirCompromisso: (c: Compromisso) => void;
  aoAbrirAvulso: (a: AtendimentoAvulsoAgenda) => void;
  aoAbrirAtividade?: (a: AtividadeAvulsaAgenda) => void;
}) {
  const dias = diasVisiveis(escala, datasDosItens([...compromissos, ...avulsos, ...atividades]));
  // Sem nenhuma faixa na semana, a grade mantém o expediente padrão.
  const comFaixa = dias.filter((d) => d.inicio != null && d.fim != null);
  const abertura = comFaixa.length ? Math.min(...comFaixa.map((d) => minutos(d.inicio!))) : 7 * 60;
  // Um item que termina depois do fechamento (tempo extra) alarga a grade em vez de ser cortado.
  const fimDosItens = [...compromissos, ...avulsos, ...atividades]
    .filter((it) => dias.some((d) => d.data === diaCivil(it.inicio)))
    .map((it) => (diaCivil(it.fim) === diaCivil(it.inicio) ? minutos(horaCivil(it.fim)) : 24 * 60));
  const fechamento = Math.max(comFaixa.length ? Math.max(...comFaixa.map((d) => minutos(d.fim!))) : 17 * 60, ...fimDosItens);
  const horas: number[] = [];
  for (let m = abertura; m < fechamento; m += 60) horas.push(m);
  const total = fechamento - abertura;
  const altura = total * PX_POR_MINUTO;
  /** Posição vertical em % da grade: acompanha a altura real das colunas. */
  const pct = (min: number): string => `${((min - abertura) / total) * 100}%`;
  const hoje = hojeCivil(agora);
  const agoraMin = minutos(horaCivil(agora.toISOString()));

  // Profissionais fora da lista (ex.: inativos) ganham sub-coluna no fim, para nenhum bloco sumir.
  const pistas = equipe ? [...equipe] : [];
  if (equipe) {
    for (const it of [...compromissos, ...avulsos, ...atividades]) {
      if (!pistas.some((p) => p.id === it.profissionalId)) pistas.push({ id: it.profissionalId, nome: it.profissional });
    }
  }

  const itens: Item[] = [
    ...compromissos.map((c): Item => {
      const estilo = estiloCompromisso(c, agora);
      const quem = c.aluno ?? c.titulo;
      const remarcado = !!c.remarcadoDeId;
      const texto = equipe ? iniciais(quem) : quem;
      // O nome acessível começa pelo texto visível do bloco (WCAG 2.5.3); na equipe o nome completo vem depois das iniciais.
      const rotulo = [
        texto,
        `${formatarHora(c.inicio)}–${formatarHora(c.fim)}`,
        ROTULO_TIPO[c.tipo],
        ...(equipe ? [quem] : []),
        estilo.rotuloEstado.toLowerCase(),
        ...(remarcado ? ["remarcado de outro horário"] : []),
        ...(equipe ? [`com ${c.profissional}`] : []),
      ].join(", ");
      return {
        ...c,
        lembrete: c.tipo === "Lembrete",
        render: (posicao) => (
          <BlocoCompromisso
            key={c.id}
            rotulo={rotulo}
            texto={texto}
            detalhe={equipe ? undefined : c.aluno ? `${c.titulo} · ${estilo.rotuloEstado}` : estilo.rotuloEstado}
            dica={equipe ? `${quem} · ${c.profissional}` : quem}
            estilo={estilo}
            riscado={c.estado === "Cancelado" || c.estado === "Remarcado"}
            lembrete={c.tipo === "Lembrete"}
            remarcado={remarcado}
            posicao={posicao}
            aoAbrir={() => aoAbrirCompromisso(c)}
          />
        ),
      };
    }),
    ...avulsos.map((a): Item => {
      const estilo = estiloAtendimento(a.estado);
      const texto = equipe ? iniciais(a.aluno) : a.aluno;
      const rotulo = [
        texto,
        `${formatarHora(a.inicio)}–${formatarHora(a.fim)}`,
        ROTULO_TIPO_ATENDIMENTO[a.tipo],
        ...(equipe ? [a.aluno] : []),
        estilo.rotuloEstado.toLowerCase(),
        "atendimento avulso",
        ...(equipe ? [`com ${a.profissional}`] : []),
      ].join(", ");
      return {
        ...a,
        lembrete: false,
        render: (posicao) => (
          <BlocoCompromisso
            key={a.id}
            rotulo={rotulo}
            texto={texto}
            detalhe={equipe ? undefined : `${ROTULO_TIPO_ATENDIMENTO[a.tipo]} · ${estilo.rotuloEstado}`}
            dica={equipe ? `${a.aluno} · ${a.profissional}` : a.aluno}
            estilo={estilo}
            posicao={posicao}
            aoAbrir={() => aoAbrirAvulso(a)}
          />
        ),
      };
    }),
    ...atividades.map((a): Item => {
      const tipo = ROTULO_TIPO_ATIVIDADE[a.tipo];
      const rotulo = [
        tipo,
        `${formatarHora(a.inicio)}–${formatarHora(a.fim)}`,
        "Atividade no curso",
        "atividade avulsa",
        ...(equipe ? [`com ${a.profissional}`] : []),
      ].join(", ");
      return {
        ...a,
        lembrete: false,
        render: (posicao) => (
          <BlocoCompromisso
            key={a.id}
            rotulo={rotulo}
            texto={tipo}
            detalhe={equipe ? undefined : "Atividade no curso"}
            dica={equipe ? `${tipo} · ${a.profissional}` : tipo}
            estilo={{ variante: "atividade", padrao: "solido", rotuloEstado: "Registrada" }}
            posicao={posicao}
            aoAbrir={() => aoAbrirAtividade?.(a)}
          />
        ),
      };
    }),
  ].sort((a, b) => a.inicio.localeCompare(b.inicio));

  function posicao(it: Item, dia: string): CSSProperties {
    const { lembrete } = it;
    // Fora do expediente (dado antigo ou corrigido): preso à borda da grade com altura mínima, nunca um botão de altura zero.
    const fimMin = diaCivil(it.fim) === dia ? minutos(horaCivil(it.fim)) : 24 * 60;
    const fim = Math.min(total, Math.max(fimMin - abertura, 0));
    const inicio = Math.min(Math.max(0, minutos(horaCivil(it.inicio)) - abertura), fim, total - ALTURA_MINIMA);
    const n = equipe ? pistas.length : 1;
    const pista = equipe ? pistas.findIndex((p) => p.id === it.profissionalId) : 0;
    const largura = 100 / n;
    // Lembrete não ocupa horário: meia largura, à direita, sobre o bloco que houver.
    const esquerda = pista * largura + (lembrete ? largura / 2 : 0);
    return {
      top: pct(abertura + inicio),
      height: `${(Math.max(fim - inicio, ALTURA_MINIMA) / total) * 100}%`,
      left: `calc(${esquerda}% + 2px)`,
      width: `calc(${lembrete ? largura / 2 : largura}% - 4px)`,
    };
  }

  return (
    <div
      style={{ "--colunas": dias.length } as CSSProperties}
      className="grid flex-1 grid-cols-[48px_minmax(0,1fr)] grid-rows-[auto_1fr] xl:grid-cols-[48px_repeat(var(--colunas),minmax(0,1fr))]"
    >
      <div className="border-b border-borda" />
      {dias.map((d, i) => {
        const ehHoje = d.data === hoje;
        const desabilitado = !!d.feriado || d.inicio == null;
        return (
          <div
            key={d.data}
            className={cn(
              "border-b border-l border-borda px-2.5 py-2.5 text-xs font-medium",
              i !== diaMovel && "max-xl:hidden",
              ehHoje ? "bg-realce font-bold text-primaria" : desabilitado || d.semAtendimento ? "text-suave" : "text-texto",
            )}
          >
            {formatarDiaSemana(d.data)}
            {ehHoje && " · hoje"}
            {d.feriado ? ` · feriado: ${d.feriado}` : d.inicio == null ? " · fora da escala" : d.semAtendimento && " · sem atendimento"}
            {equipe && (
              <div className="-mx-2.5 mt-1 flex font-normal text-suave">
                {pistas.map((p) => (
                  <span key={p.id} title={p.nome} className="min-w-0 flex-1 truncate text-center text-mini">
                    <span aria-hidden="true">{iniciais(p.nome)}</span>
                    <span className="sr-only">{p.nome}</span>
                  </span>
                ))}
              </div>
            )}
          </div>
        );
      })}

      <div className="relative text-right text-mini text-suave" style={{ minHeight: altura }} aria-hidden="true">
        {horas.map((m) => (
          <div key={m} className="absolute right-2" style={{ top: pct(m) }}>
            {String(m / 60).padStart(2, "0")}h
          </div>
        ))}
      </div>
      {dias.map((d, i) => {
        const dia = d.data;
        const ehHoje = dia === hoje;
        const doDia = itens.filter((it) => diaCivil(it.inicio) === dia);
        // Feriado e dia sem faixa não aceitam agendamento (a API recusa); "sem atendimento" só sinaliza.
        const aviso = d.feriado ? `Feriado: ${d.feriado}` : d.inicio == null ? "Fora da escala do profissional" : d.semAtendimento ? "Sem atendimento na escala" : null;
        return (
          <div
            key={dia}
            role="group"
            aria-label={`${nomeDia(dia)}, ${formatarDataCivil(dia)}${aviso ? `, ${aviso.toLowerCase()}` : ""}`}
            style={{ minHeight: altura }}
            className={cn(
              "relative border-l border-borda",
              i !== diaMovel && "max-xl:hidden",
              ehHoje && "bg-linha",
              (aviso || d.semAtendimento) &&
                "[background-image:repeating-linear-gradient(135deg,var(--color-hachura)_0_8px,var(--color-superficie)_8px_16px)]",
            )}
          >
            {horas.map((m) => (
              <div key={m} aria-hidden="true" className="absolute inset-x-0 border-t border-borda/60" style={{ top: pct(m) }} />
            ))}
            {aviso && (
              <p className="absolute inset-x-2 top-2 rounded-sm bg-superficie/90 px-1.5 py-1 text-center text-mini text-suave">{aviso}</p>
            )}
            {doDia.map((it) => it.render(posicao(it, dia)))}
            {ehHoje && agoraMin >= abertura && agoraMin <= fechamento && (
              <div aria-hidden="true" className="pointer-events-none absolute inset-x-0 z-30 h-0.5 bg-urgente" style={{ top: pct(agoraMin) }} />
            )}
          </div>
        );
      })}
    </div>
  );
}
