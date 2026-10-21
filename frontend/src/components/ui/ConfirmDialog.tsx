import type { ReactNode } from "react";
import { Button } from "./Button";
import { Modal } from "./Modal";

export function ConfirmDialog({
  aberto,
  titulo,
  mensagem,
  confirmar,
  aoConfirmar,
  aoCancelar,
  carregando,
  children,
}: {
  aberto: boolean;
  titulo: string;
  mensagem: string;
  confirmar: string;
  aoConfirmar: () => void;
  aoCancelar: () => void;
  carregando?: boolean;
  /** Conteúdo extra abaixo da mensagem (ex.: campo de motivo). */
  children?: ReactNode;
}) {
  return (
    <Modal
      aberto={aberto}
      aoFechar={aoCancelar}
      titulo={titulo}
      bloqueado={carregando}
      rodape={
        <>
          <Button variante="secundaria" onClick={aoCancelar} disabled={carregando}>
            Cancelar
          </Button>
          <Button variante="perigo" onClick={aoConfirmar} carregando={carregando}>
            {confirmar}
          </Button>
        </>
      }
    >
      <p className="text-sm">{mensagem}</p>
      {children && <div className="mt-4 flex flex-col gap-4">{children}</div>}
    </Modal>
  );
}
