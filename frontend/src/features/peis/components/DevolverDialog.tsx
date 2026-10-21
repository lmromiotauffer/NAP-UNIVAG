import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert } from "../../../components/ui/Alert";
import { Button } from "../../../components/ui/Button";
import { Field } from "../../../components/ui/Field";
import { Modal } from "../../../components/ui/Modal";
import { Textarea } from "../../../components/ui/Textarea";
import { tratarErro } from "../../../lib/api";
import type { VersaoPeiDetalhe } from "../../../lib/types";
import { useAcaoVersao } from "../hooks/usePeis";

const esquema = z.object({
  texto: z.string().trim().min(3, "Informe o texto (de 3 a 500 caracteres).").max(500, "Informe o texto (de 3 a 500 caracteres)."),
});
type Entrada = z.input<typeof esquema>;
type Saida = z.output<typeof esquema>;

/** Diálogo de texto obrigatório (3–500) para devolver ou arquivar. Montar só enquanto aberto: a versão é a da abertura. */
export function DialogoMotivoPei({
  versao: dto,
  acao,
  titulo,
  rotulo,
  campoApi,
  botao,
  perigo,
  aoFechar,
  aoConcluir,
}: {
  versao: VersaoPeiDetalhe;
  acao: "devolver" | "arquivar";
  titulo: string;
  rotulo: string;
  campoApi: string;
  botao: string;
  perigo?: boolean;
  aoFechar: () => void;
  aoConcluir: () => void;
}) {
  const [versao] = useState(dto.versao);
  const [erroGeral, setErroGeral] = useState<string | null>(null);
  const mutacao = useAcaoVersao();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Entrada, unknown, Saida>({ resolver: zodResolver(esquema), defaultValues: { texto: "" } });

  async function enviar({ texto }: Saida) {
    setErroGeral(null);
    try {
      await mutacao.mutateAsync({ acao, id: dto.id, versao, texto });
      aoConcluir();
    } catch (e) {
      setErroGeral(tratarErro(e, ["texto"] as const, (campo, message) => setError(campo, { message }), { [campoApi]: "texto" }));
    }
  }

  return (
    <Modal aberto aoFechar={aoFechar} titulo={titulo}>
      <form noValidate onSubmit={handleSubmit(enviar)} className="flex flex-col gap-4">
        {erroGeral && <Alert variante="urgente" className="animate-surgir">{erroGeral}</Alert>}
        <Field rotulo={rotulo} erro={errors.texto?.message} obrigatorio>
          <Textarea maxLength={500} {...register("texto")} />
        </Field>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="secundaria" onClick={aoFechar} disabled={isSubmitting}>
            Voltar
          </Button>
          <Button type="submit" variante={perigo ? "perigo" : "primaria"} carregando={isSubmitting}>
            {botao}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
