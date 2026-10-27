import { useIsMutating } from "@tanstack/react-query";
import { X } from "lucide-react";
import { useEffect, useId, useRef, type KeyboardEvent, type MouseEvent, type ReactNode } from "react";
import { Button } from "./Button";

const FOCAVEIS = 'a[href], button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])';

export function Modal({ aberto, aoFechar, titulo, children, rodape, bloqueado }: { aberto: boolean; aoFechar: () => void; titulo: string; children: ReactNode; rodape?: ReactNode; bloqueado?: boolean }) {
  // Envio em voo trava o fechamento: o `bloqueado` explícito (ex.: ConfirmDialog) tem precedência.
  const mutando = useIsMutating() > 0;
  const bloqueadoEfetivo = bloqueado ?? mutando;
  const ref = useRef<HTMLDialogElement>(null);
  const idTitulo = useId();
  // Seleção arrastada de um campo até o fundo termina num click no <dialog>: só fecha se o pointerdown também foi no fundo.
  const pressionouFundo = useRef(false);

  useEffect(() => {
    const dialogo = ref.current;
    if (!dialogo || !aberto) return;
    const origem = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    if (!dialogo.open) dialogo.showModal();
    // O primeiro focável é o botão "Fechar": o foco inicial vai ao primeiro campo ou ação do conteúdo, e só cai no X se não houver outro.
    const focaveis = Array.from(dialogo.querySelectorAll<HTMLElement>(FOCAVEIS));
    (focaveis.find((el) => el.getAttribute("aria-label") !== "Fechar") ?? focaveis[0])?.focus();
    return () => {
      if (dialogo.open) dialogo.close();
      origem?.focus();
    };
  }, [aberto]);

  // Esc: trata o keydown (nativo e jsdom) e bloqueia o "cancel" nativo para não fechar duas vezes.
  function aoTeclar(e: KeyboardEvent<HTMLDialogElement>) {
    if (e.key !== "Escape") return;
    e.preventDefault();
    if (!bloqueadoEfetivo) aoFechar();
  }

  function aoClicar(e: MouseEvent<HTMLDialogElement>) {
    const noFundo = pressionouFundo.current && e.target === e.currentTarget;
    pressionouFundo.current = false;
    if (noFundo && !bloqueadoEfetivo) aoFechar();
  }

  return (
    <dialog
      ref={ref}
      aria-labelledby={idTitulo}
      onKeyDown={aoTeclar}
      onCancel={(e) => {
        e.preventDefault();
        if (!bloqueadoEfetivo) aoFechar();
      }}
      onPointerDown={(e) => {
        pressionouFundo.current = e.target === e.currentTarget;
      }}
      onClick={aoClicar}
      // rounded-xl: mesmo raio das outras camadas flutuantes (menu "Mais", lista do select); o 8px destoava dos cartões (14–20px).
      className="m-auto w-full max-w-lg rounded-xl border border-borda bg-superficie p-0 text-texto backdrop:bg-bloco/60"
    >
      {aberto && (
        <div className="flex flex-col gap-4 p-5">
          <div className="flex items-start justify-between gap-4">
            <h2 id={idTitulo} className="min-w-0 font-titulo text-lg font-bold break-words">
              {titulo}
            </h2>
            <Button tamanho="icone" variante="fantasma" aria-label="Fechar" onClick={aoFechar} disabled={bloqueadoEfetivo}>
              <X aria-hidden="true" className="size-4" />
            </Button>
          </div>
          <div>{children}</div>
          {/* flex-wrap: com texto grande os botões descem de linha em vez de vazar para fora do diálogo. */}
          {rodape && <div className="flex flex-wrap justify-end gap-2">{rodape}</div>}
        </div>
      )}
    </dialog>
  );
}
