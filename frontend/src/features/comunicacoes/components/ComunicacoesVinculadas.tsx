import { Mail } from "lucide-react";
import { useId, useState } from "react";
import { Button } from "../../../components/ui/Button";
import { Select } from "../../../components/ui/Select";
import { classeMolduraRolavel } from "../../../components/ui/Table";
import { CAIXAS, ROTULO_CAIXA, type Caixa } from "../rotulos";
import { CaixaMensagens } from "./CaixaMensagens";

const TAMANHO_LISTA = 10;

/** Mensagens vinculadas a um aluno ou encaminhamento, nas caixas do próprio usuário (a API só devolve as de que ele participa). */
export function ComunicacoesVinculadas({
  vinculo,
  descricaoVazia,
  aoEscrever,
}: {
  vinculo: { alunoId: string } | { encaminhamentoId: string };
  descricaoVazia: string;
  aoEscrever?: () => void;
}) {
  const [caixa, setCaixa] = useState<Caixa>("Entrada");
  const [pagina, setPagina] = useState(1);
  const id = useId();
  return (
    <section aria-labelledby={`${id}-titulo`} className={classeMolduraRolavel}>
      <div className="flex flex-wrap items-center justify-between gap-2 px-[22px] pt-4 pb-2">
        <h2 id={`${id}-titulo`} className="m-0 font-titulo text-base font-bold">
          Comunicações
        </h2>
        <div className="flex flex-wrap items-center gap-2">
          <label htmlFor={`${id}-caixa`} className="sr-only">
            Caixa
          </label>
          <Select
            id={`${id}-caixa`}
            value={caixa}
            onChange={(e) => {
              setCaixa(e.target.value as Caixa);
              setPagina(1);
            }}
            className="h-8 w-auto py-0 text-xs"
          >
            {CAIXAS.map((c) => (
              <option key={c} value={c}>
                {ROTULO_CAIXA[c]}
              </option>
            ))}
          </Select>
          {aoEscrever && (
            <Button tamanho="sm" onClick={aoEscrever}>
              Nova mensagem
            </Button>
          )}
        </div>
      </div>
      <CaixaMensagens
        filtro={{ caixa, ...vinculo, pagina, tamanho: TAMANHO_LISTA }}
        comBusca={false}
        aoMudarPagina={setPagina}
        vazio={{ icone: <Mail />, titulo: "Nenhuma mensagem", descricao: descricaoVazia }}
      />
    </section>
  );
}
