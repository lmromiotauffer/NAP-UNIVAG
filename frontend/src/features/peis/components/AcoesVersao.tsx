import { useState } from "react";
import { useNavigate } from "react-router";
import { pode } from "../../../auth/capacidades";
import { useSessao } from "../../../auth/useSessao";
import { Button } from "../../../components/ui/Button";
import { ConfirmDialog } from "../../../components/ui/ConfirmDialog";
import { useToast } from "../../../components/ui/Toast";
import { mensagemErro } from "../../../lib/api";
import type { VersaoPeiDetalhe } from "../../../lib/types";
import { useAcaoVersao, useNovaVersaoPei, type AcaoVersao } from "../hooks/usePeis";
import { TEXTO_CIENCIA } from "../rotulos";
import { peiService } from "../services/peiService";
import { AcessoAlunoDialog } from "./AcessoAlunoDialog";
import { DialogoMotivoPei } from "./DevolverDialog";
import { TrocarResponsavelDialog } from "./TrocarResponsavelDialog";

type Dialogo = "devolver" | "arquivar" | "acesso" | "descartar" | "responsavel" | null;

/** Transições conforme `versao.acoes`; "Enviar para revisão" mora no formulário de conteúdo. "Trocar responsável", "Descartar rascunho" e "Baixar PDF" seguem permissão/estado e a API decide. */
export function AcoesVersao({ versao }: { versao: VersaoPeiDetalhe }) {
  const { usuario } = useSessao();
  const toast = useToast();
  const navegar = useNavigate();
  const acao = useAcaoVersao();
  const nova = useNovaVersaoPei();
  const [dialogo, setDialogo] = useState<Dialogo>(null);
  const [chaveNova] = useState(() => crypto.randomUUID());
  const [baixando, setBaixando] = useState(false);
  const a = versao.acoes;
  // O DTO não traz flag de descarte: a tela mostra para a Equipe NAP e a API decide.
  const podeDescartar = versao.estado === "Rascunho" && versao.numero > 1 && pode(usuario, "alunos.gerenciar");
  const fechar = () => setDialogo(null);
  const concluir = (msg: string) => () => {
    toast.sucesso(msg);
    setDialogo(null);
  };

  async function executar(qual: AcaoVersao, msg: string) {
    try {
      await acao.mutateAsync({ acao: qual, id: versao.id, versao: versao.versao });
      toast.sucesso(msg);
    } catch (e) {
      toast.erro(mensagemErro(e));
    }
  }

  async function descartar() {
    try {
      await acao.mutateAsync({ acao: "descartar", id: versao.id, versao: versao.versao });
      toast.sucesso("Rascunho descartado.");
      void navegar(`/alunos/${versao.alunoId}`);
    } catch (e) {
      setDialogo(null);
      toast.erro(mensagemErro(e));
    }
  }

  async function criarNovaVersao() {
    try {
      const { id } = await nova.mutateAsync({ peiId: versao.peiId, chave: chaveNova });
      void navegar(`/peis/versoes/${id}`);
    } catch (e) {
      toast.erro(mensagemErro(e));
    }
  }

  async function baixar() {
    setBaixando(true);
    try {
      await peiService.baixarPdf(versao.id);
    } catch (e) {
      toast.erro(mensagemErro(e));
    } finally {
      setBaixando(false);
    }
  }

  const ocupado = acao.isPending || nova.isPending;
  // Só o botão da ação em voo mostra o spinner; os demais ficam só desabilitados.
  const executando = (qual: AcaoVersao) => acao.isPending && acao.variables?.acao === qual;
  return (
    <>
      <div className="flex flex-wrap gap-2">
        {a.podeAprovar && (
          <Button className="rounded-full" carregando={executando("aprovar")} disabled={ocupado} onClick={() => void executar("aprovar", "Revisão aprovada. As assinaturas foram liberadas.")}>
            Aprovar revisão
          </Button>
        )}
        {a.podeDevolver && (
          <Button variante="secundaria" className="rounded-full" disabled={ocupado} onClick={() => setDialogo("devolver")}>
            Devolver para ajustes
          </Button>
        )}
        {a.podeAssinarComoProfissional && (
          <Button className="rounded-full" carregando={executando("assinar")} disabled={ocupado} onClick={() => void executar("assinar", "Assinatura registrada.")}>
            Assinar como profissional
          </Button>
        )}
        {a.podeGerarAcessoAluno && (
          <Button variante="secundaria" className="rounded-full" disabled={ocupado} onClick={() => setDialogo("acesso")}>
            Gerar acesso do aluno
          </Button>
        )}
        {a.podePublicar && (
          <Button className="rounded-full" carregando={executando("publicar")} disabled={ocupado} onClick={() => void executar("publicar", "PEI publicado.")}>
            Publicar
          </Button>
        )}
        {a.podeRegistrarCiencia && (
          <Button className="rounded-full" carregando={executando("ciencia")} disabled={ocupado} onClick={() => void executar("ciencia", "Ciência registrada.")}>
            Registrar ciência
          </Button>
        )}
        {a.podeCriarNovaVersao && (
          <Button variante="secundaria" className="rounded-full" carregando={nova.isPending} disabled={ocupado} onClick={() => void criarNovaVersao()}>
            Criar nova versão
          </Button>
        )}
        {a.podeArquivar && (
          <Button variante="secundaria" className="rounded-full text-urgente" disabled={ocupado} onClick={() => setDialogo("arquivar")}>
            Arquivar
          </Button>
        )}
        {pode(usuario, "peis.publicar") && (
          <Button variante="secundaria" className="rounded-full" disabled={ocupado} onClick={() => setDialogo("responsavel")}>
            Trocar responsável
          </Button>
        )}
        {podeDescartar && (
          <Button variante="secundaria" className="rounded-full text-urgente" disabled={ocupado} onClick={() => setDialogo("descartar")}>
            Descartar rascunho
          </Button>
        )}
        <Button variante="secundaria" className="rounded-full" carregando={baixando} disabled={ocupado} onClick={() => void baixar()}>
          Baixar PDF
        </Button>
      </div>
      {a.podeRegistrarCiencia && <p className="m-0 text-xs text-suave">{TEXTO_CIENCIA}</p>}
      {dialogo === "devolver" && (
        <DialogoMotivoPei versao={versao} aoFechar={fechar} aoConcluir={concluir("Versão devolvida para ajustes.")} acao="devolver" titulo="Devolver para ajustes" rotulo="Justificativa" campoApi="justificativa" botao="Devolver" />
      )}
      {dialogo === "arquivar" && (
        <DialogoMotivoPei versao={versao} aoFechar={fechar} aoConcluir={concluir("Versão arquivada.")} acao="arquivar" titulo="Arquivar versão" rotulo="Motivo" campoApi="motivo" botao="Arquivar" perigo />
      )}
      {dialogo === "responsavel" && <TrocarResponsavelDialog versao={versao} aoFechar={fechar} aoConcluir={concluir("Responsável do PEI trocado.")} />}
      {dialogo === "acesso" && <AcessoAlunoDialog versaoId={versao.id} aoFechar={fechar} />}
      <ConfirmDialog
        aberto={dialogo === "descartar"}
        titulo="Descartar rascunho"
        mensagem={`A versão ${versao.numero} em rascunho será excluída. Esta ação não pode ser desfeita.`}
        confirmar="Descartar"
        aoConfirmar={() => void descartar()}
        aoCancelar={fechar}
        carregando={ocupado}
      />
    </>
  );
}
