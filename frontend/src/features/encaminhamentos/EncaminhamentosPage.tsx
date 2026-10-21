import { Plus, Send } from "lucide-react";
import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { Button } from "../../components/ui/Button";
import { classeIconeTitulo, classeTituloPagina } from "../../components/ui/Card";
import { Modal } from "../../components/ui/Modal";
import { classeMolduraRolavel } from "../../components/ui/Table";
import { useToast } from "../../components/ui/Toast";
import { cn } from "../../lib/cn";
import { temFiltroAtivo } from "../../lib/queryKeys";
import { EncaminhamentoForm } from "./components/EncaminhamentoForm";
import { CHAVES_FILTRO, FiltrosEncaminhamentos, filtroDaUrl } from "./components/FiltrosEncaminhamentos";
import { ListaEncaminhamentos } from "./components/ListaEncaminhamentos";

export function EncaminhamentosPage() {
  const { usuario } = useSessao();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const toast = useToast();
  const [criando, setCriando] = useState(false);
  const podeCriar = pode(usuario, "encaminhamentos.criar");

  function irParaPagina(pagina: number) {
    setParams((atual) => {
      const proximo = new URLSearchParams(atual);
      if (pagina > 1) proximo.set("pagina", String(pagina));
      else proximo.delete("pagina");
      return proximo;
    });
  }

  return (
    <>
      <title>Encaminhamentos · NAP</title>
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className={cn(classeTituloPagina, "flex items-center gap-3")}>
          <span aria-hidden="true" className={classeIconeTitulo}>
            <Send className="size-5" />
          </span>
          Encaminhamentos
        </h1>
        {podeCriar && (
          // No celular só o ícone, na linha do título; o texto continua como nome acessível.
          <Button onClick={() => setCriando(true)} title="Novo encaminhamento" className="rounded-full max-sm:px-3">
            <Plus aria-hidden="true" className="size-4" />
            <span className="max-sm:sr-only">Novo encaminhamento</span>
          </Button>
        )}
      </header>
      <FiltrosEncaminhamentos />
      <section aria-label="Lista de encaminhamentos" className={classeMolduraRolavel}>
        <ListaEncaminhamentos
          filtro={filtroDaUrl(params)}
          comFiltro={temFiltroAtivo(params, CHAVES_FILTRO)}
          aoMudarPagina={irParaPagina}
          vazio={{
            titulo: "Nenhum encaminhamento",
            descricao: "Os encaminhamentos ao SIP, à Clínica Escola, a serviços externos e à coordenação de curso aparecem aqui.",
            acaoFiltro: (
              <Button variante="secundaria" tamanho="sm" onClick={() => setParams({})}>
                Limpar filtros
              </Button>
            ),
          }}
        />
      </section>
      <p className="m-0 text-rotulo text-suave">
        O alerta de prazo não substitui o estado real do encaminhamento. O SIP atualiza apenas o progresso dos casos destinados ao serviço.
      </p>
      {podeCriar && (
        <Modal aberto={criando} aoFechar={() => setCriando(false)} titulo="Novo encaminhamento">
          <EncaminhamentoForm
            aoCancelar={() => setCriando(false)}
            aoConcluir={(id) => {
              toast.sucesso("Encaminhamento registrado.");
              void navigate(`/encaminhamentos/${id}`);
            }}
          />
        </Modal>
      )}
    </>
  );
}
