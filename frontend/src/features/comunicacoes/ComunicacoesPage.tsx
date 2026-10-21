import { Archive, FileText, Inbox, Mail, Plus, Send } from "lucide-react";
import { useState, type ReactNode } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { BuscaUrl } from "../../components/ui/BuscaUrl";
import { Button } from "../../components/ui/Button";
import { classeIconeTitulo, classeTituloPagina } from "../../components/ui/Card";
import { ListaAbas } from "../../components/ui/ListaAbas";
import { Modal } from "../../components/ui/Modal";
import { classeMolduraRolavel } from "../../components/ui/Table";
import { useToast } from "../../components/ui/Toast";
import { cn } from "../../lib/cn";
import { comParametro, paginaDaUrl } from "../../lib/queryKeys";
import { AbaNotificacoes } from "./components/AbaNotificacoes";
import { CaixaMensagens } from "./components/CaixaMensagens";
import { EscreverMensagem } from "./components/EscreverMensagem";
import { CAIXAS, ROTULO_CAIXA, type Caixa } from "./rotulos";

type Aba = Lowercase<Caixa> | "notificacoes";

const ABAS: { chave: Aba; rotulo: string; caixa?: Caixa }[] = [
  ...CAIXAS.map((c) => ({ chave: c.toLowerCase() as Aba, rotulo: ROTULO_CAIXA[c], caixa: c })),
  { chave: "notificacoes", rotulo: "Notificações" },
];

const VAZIO: Record<Caixa, { icone: ReactNode; titulo: string; descricao: string }> = {
  Entrada: { icone: <Inbox />, titulo: "Nenhuma mensagem", descricao: "As mensagens recebidas aparecem aqui." },
  Enviados: { icone: <Send />, titulo: "Nenhuma mensagem enviada", descricao: "As mensagens que você enviar aparecem aqui." },
  Rascunhos: { icone: <FileText />, titulo: "Nenhum rascunho", descricao: "Mensagens salvas sem enviar ficam aqui até você enviá-las ou descartá-las." },
  Arquivadas: { icone: <Archive />, titulo: "Nenhuma mensagem arquivada", descricao: "Arquivar tira a mensagem da Entrada ou de Enviados só para você." },
};

export function ComunicacoesPage() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const toast = useToast();
  const [escrevendo, setEscrevendo] = useState(false);
  const aba = ABAS.find((a) => a.chave === params.get("aba")) ?? ABAS[0]!;
  const busca = params.get("busca") ?? "";
  const pagina = paginaDaUrl(params) ?? 1;

  // Trocar de aba descarta busca, página e filtros da aba anterior.
  const trocarAba = (chave: Aba) => setParams(chave === "entrada" ? {} : { aba: chave });

  const alterar = (chave: string, valor: string | null) => setParams((atual) => comParametro(atual, chave, valor));

  return (
    <>
      <title>Comunicações · NAP</title>
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className={cn(classeTituloPagina, "flex items-center gap-3")}>
          <span aria-hidden="true" className={classeIconeTitulo}>
            <Mail className="size-5" />
          </span>
          Comunicações
        </h1>
        {/* No celular só o ícone, na linha do título; o texto continua como nome acessível. */}
        <Button onClick={() => setEscrevendo(true)} title="Nova mensagem" className="rounded-full max-sm:px-3">
          <Plus aria-hidden="true" className="size-4" />
          <span className="max-sm:sr-only">Nova mensagem</span>
        </Button>
      </header>

      <ListaAbas abas={ABAS} atual={aba.chave} aoTrocar={trocarAba} rotulo="Caixas de comunicação" />
      {/* `key`: o painel remonta ao trocar de aba e chega com o animate-surgir do <main>, junto com o sublinhado que desliza. */}
      <div key={aba.chave} role="tabpanel" id={`painel-${aba.chave}`} aria-labelledby={`aba-${aba.chave}`} className="flex flex-col gap-[18px] md:min-h-0 md:flex-1">
        {aba.caixa ? (
          <>
            <BuscaUrl id="busca-caixa" rotulo="Buscar no assunto ou no texto" valor={busca} aoMudar={(v) => alterar("busca", v)} maxLength={100} className="max-w-sm" />
            <section aria-label={`Mensagens: ${aba.rotulo}`} className={classeMolduraRolavel}>
              <CaixaMensagens
                filtro={{ caixa: aba.caixa, busca: busca || undefined, pagina }}
                comBusca={!!busca}
                vazio={VAZIO[aba.caixa]}
                aoMudarPagina={(p) => alterar("pagina", p > 1 ? String(p) : null)}
              />
            </section>
          </>
        ) : (
          <AbaNotificacoes />
        )}
      </div>

      <Modal aberto={escrevendo} aoFechar={() => setEscrevendo(false)} titulo="Nova mensagem">
        {escrevendo && (
          <EscreverMensagem
            aoCancelar={() => setEscrevendo(false)}
            aoConcluir={(acao, id) => {
              setEscrevendo(false);
              if (acao === "enviada") {
                toast.sucesso("Mensagem enviada.");
                void navigate(`/comunicacoes/${id}`);
              } else {
                toast.sucesso("Rascunho salvo.");
                trocarAba("rascunhos");
              }
            }}
          />
        )}
      </Modal>
    </>
  );
}
