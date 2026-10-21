import { ClipboardCheck, FilePen, PenLine, type LucideIcon } from "lucide-react";
import { useId } from "react";
import { formatarInteiro } from "../../../lib/format";
import type { Painel } from "../../../lib/types";
import { CartaoIndicador } from "./CartaoIndicador";

type Cartao = { titulo: string; valor: number; base: string; Icone: LucideIcon; classeIcone: string };

/** PEIs no escopo do perfil: a API só envia o bloco a quem tem acesso (coordenação de curso conta só os publicados do curso). */
export function BlocoPei({ dados }: { dados: NonNullable<Painel["peis"]> }) {
  const id = useId();
  const cartoes: Cartao[] = [
    { titulo: "PEIs em elaboração", valor: dados.emElaboracao, base: "versões em rascunho ou em revisão", Icone: FilePen, classeIcone: "text-info" },
    { titulo: "Aguardando assinaturas", valor: dados.aguardandoAssinaturas, base: "revisão aprovada, faltam assinaturas", Icone: PenLine, classeIcone: "text-aguardando-texto" },
    { titulo: "Publicados sem ciência", valor: dados.publicadosSemCiencia, base: "a coordenação ainda não registrou ciência", Icone: ClipboardCheck, classeIcone: "text-urgente" },
  ];
  return (
    <section aria-labelledby={id} className="flex min-w-0 flex-col gap-2">
      <h2 id={id} className="m-0 font-titulo text-destaque font-bold">
        PEI
      </h2>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 sm:gap-3">
        {cartoes.map((c, i) => (
          // No celular (2 colunas) o 3º cartão ocupa a linha inteira em vez de sobrar meio vazio, como o de Mensagens.
          <CartaoIndicador key={c.titulo} titulo={c.titulo} valor={c.valor} formatar={formatarInteiro} base={c.base} Icone={c.Icone} classeIcone={c.classeIcone} className={i === 2 ? "max-sm:col-span-2" : undefined} />
        ))}
      </div>
    </section>
  );
}
