import { BarChart3, CalendarDays, ClipboardList, LayoutGrid, Mail, Presentation, Send, Settings, Users, type LucideIcon } from "lucide-react";
import { Suspense, useEffect, useRef, useState } from "react";
import { Outlet, useLocation } from "react-router";
import { pode, rotuloPerfil, type Capacidade } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { useContagemMensagens } from "../../features/comunicacoes/hooks/useComunicacoes";
import { useContagemNotificacoes } from "../../features/notificacoes/hooks/useNotificacoes";
import { Carregando } from "../ui/Carregando";
import { Modal } from "../ui/Modal";
import { Skeleton } from "../ui/Skeleton";
import { BarraInferior } from "./BarraInferior";
import { TrilhoNavegacao } from "./TrilhoNavegacao";

/** `rotuloCurto` é só da barra inferior (320 px); o nome completo segue acessível. `contador` mostra o selo de não lidas. `noMais` tira o item da barra inferior (no máximo 6 botões, para caberem com folga em 320 px) e o oferece no menu "Mais". */
export type ItemNav = { rota: string; rotulo: string; rotuloCurto?: string; dica: string; Icone: LucideIcon; capacidade: Capacidade; contador?: boolean; noMais?: boolean };

const ITENS_NAV: ItemNav[] = [
  { rota: "/", rotulo: "Painel", dica: "Visão geral do dia: pendências, equipe e números da semana", Icone: LayoutGrid, capacidade: "painel" },
  { rota: "/alunos", rotulo: "Alunos", dica: "Lista e ficha de cada aluno acompanhado", Icone: Users, capacidade: "alunos.consultar" },
  { rota: "/agenda", rotulo: "Agenda", dica: "Agenda semanal da equipe: atendimentos, reuniões e bloqueios", Icone: CalendarDays, capacidade: "agenda" },
  { rota: "/atendimentos", rotulo: "Atendimentos", rotuloCurto: "Atend.", dica: "Registro e histórico de atendimentos e ausências", Icone: ClipboardList, capacidade: "atendimentos.registrar" },
  { rota: "/atividades", rotulo: "Atividades", dica: "Atividades coletivas nos cursos: rodas de conversa, reuniões pedagógicas e formações", Icone: Presentation, capacidade: "atividades", noMais: true },
  { rota: "/producao", rotulo: "Produção", dica: "Produção por período: ocorrências, tempo, capacidade e ausências", Icone: BarChart3, capacidade: "producao", noMais: true },
  { rota: "/encaminhamentos", rotulo: "Encaminhamentos", rotuloCurto: "Encaminh.", dica: "Encaminhamentos ao SIP, Clínica Escola, serviços externos e coordenação de curso", Icone: Send, capacidade: "encaminhamentos", noMais: true },
  { rota: "/comunicacoes", rotulo: "Comunicações", rotuloCurto: "Comunic.", dica: "Mensagens com a equipe, a coordenação e o SIP, e as notificações do sistema", Icone: Mail, capacidade: "comunicacoes", contador: true },
  { rota: "/administracao", rotulo: "Administração", dica: "Usuários, cursos, escalas, feriados e limites de alerta", Icone: Settings, capacidade: "administracao", noMais: true },
];

export const CORES_AJUDA = [
  ["bg-urgente", "Vermelho", "Urgente: prazo estourado, faltas e ausências."],
  ["bg-aguardando", "Âmbar", "Aguardando outra pessoa: ciência, assinatura."],
  ["bg-info", "Azul", "Em acompanhamento: atendimentos e alunos ativos no NAP."],
  ["bg-atividade", "Rosa", "Reuniões e atividades nos cursos."],
  ["bg-realizado", "Verde", "Realizado, vigente ou alta."],
  ["bg-suave", "Cinza", "Neutro: horário livre, bloqueio, item sem desfecho."],
] as const;

export function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/);
  return ((partes[0]?.[0] ?? "") + (partes.length > 1 ? (partes.at(-1)?.[0] ?? "") : "")).toUpperCase();
}

export function AppShell() {
  const { usuario, sair } = useSessao();
  const [ajudaAberta, setAjudaAberta] = useState(false);
  // Ao trocar de página, o foco vai ao <main>: o leitor de tela lê o novo título e o Tab recomeça do conteúdo (não do menu).
  const { pathname } = useLocation();
  const rotaAnterior = useRef(pathname);
  useEffect(() => {
    if (rotaAnterior.current === pathname) return;
    rotaAnterior.current = pathname;
    document.getElementById("conteudo")?.focus();
  }, [pathname]);
  // Um selo só: mensagens recebidas não lidas + notificações não lidas. Uma contagem que falhe não zera a outra.
  const comunica = pode(usuario, "comunicacoes");
  const naoLidas = (useContagemMensagens(comunica).data?.naoLidas ?? 0) + (useContagemNotificacoes(comunica).data?.naoLidas ?? 0);
  // SIP e coordenação de curso (sem agenda) têm a barra curta: Encaminhamentos, o trabalho principal deles, sai do "Mais".
  const itens = ITENS_NAV.filter((i) => pode(usuario, i.capacidade)).map((i) =>
    i.rota === "/encaminhamentos" && !pode(usuario, "agenda") ? { ...i, noMais: false } : i,
  );
  const perfil = { nome: usuario?.nome ?? "", sigla: iniciais(usuario?.nome ?? ""), rotulo: rotuloPerfil(usuario?.perfil ?? "") };

  // A RotaProtegida leva ao /entrar quando a sessão fica anônima.
  const props = { itens, naoLidas, perfil, aoSair: () => void sair(), aoAbrirAjuda: () => setAjudaAberta(true) };
  return (
    <div className="flex min-h-dvh bg-fundo text-texto">
      <a
        href="#conteudo"
        className="sr-only z-50 rounded-md bg-primaria px-3 py-2 text-primaria-texto focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
      >
        Pular para o conteúdo
      </a>
      <TrilhoNavegacao {...props} />
      <main id="conteudo" tabIndex={-1} // No desktop o main é o contêiner de rolagem (altura da viewport): a lista de uma página pode encolher e rolar por dentro.
        // [&>*]:animate-surgir: o que monta dentro do main (página nova, skeleton, conteúdo que o substitui) chega em 200 ms, sem coreografia.
        className="flex min-w-0 flex-1 flex-col gap-[18px] px-4 pt-5 pb-24 outline-none sm:px-[clamp(16px,3vw,32px)] md:h-dvh md:overflow-y-auto md:pb-6 [&>*]:animate-surgir">
        {/* Troca de usuário (ex.: login em outra aba) remonta a página: nenhum estado local do anterior sobrevive. */}
        <Suspense
          fallback={
            <Carregando rotulo="Carregando página" className="flex flex-col gap-[18px]">
              <Skeleton className="h-8 w-72 max-w-full" />
              <Skeleton className="h-64 w-full" />
            </Carregando>
          }
        >
          <Outlet key={usuario?.id} />
        </Suspense>
      </main>
      <BarraInferior {...props} />
      <Modal aberto={ajudaAberta} aoFechar={() => setAjudaAberta(false)} titulo="Como usar o sistema">
        <div className="flex flex-col gap-5 text-denso">
          <section className="flex flex-col gap-2.5">
            <h3 className="text-xs font-semibold tracking-wide text-suave uppercase">Áreas</h3>
            {itens.map(({ rota, rotulo, dica, Icone }) => (
              <div key={rota} className="flex items-start gap-2.5">
                <Icone aria-hidden="true" className="mt-0.5 size-4 flex-none text-primaria" />
                <div>
                  <strong className="font-semibold">{rotulo}</strong>
                  <div className="text-suave">{dica}</div>
                </div>
              </div>
            ))}
          </section>
          <section className="flex flex-col gap-2.5">
            <h3 className="text-xs font-semibold tracking-wide text-suave uppercase">O que as cores significam</h3>
            {CORES_AJUDA.map(([cor, nome, descricao]) => (
              <div key={nome} className="flex items-start gap-2.5">
                <span aria-hidden="true" className={`mt-[3px] size-3.5 flex-none rounded ${cor}`} />
                <div>
                  <strong className="font-semibold">{nome}</strong>
                  <div className="text-suave">{descricao}</div>
                </div>
              </div>
            ))}
          </section>
        </div>
      </Modal>
    </div>
  );
}
