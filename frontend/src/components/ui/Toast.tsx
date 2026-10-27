import { X } from "lucide-react";
import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { cn } from "../../lib/cn";
import { Alert } from "./Alert";

type Aviso = { id: number; tipo: "sucesso" | "erro"; msg: string; saindo?: boolean };
type ContextoToast = { sucesso: (msg: string) => void; erro: (msg: string) => void };

const Contexto = createContext<ContextoToast | null>(null);
const DURACAO_MS = 5000;
// Saída em 150 ms (mais curta que a chegada de 200 ms): o aviso esmaece em vez de sumir de uma vez. Mesmo valor de --animate-sumir.
const SAIDA_MS = 150;
let proximoId = 0;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [avisos, setAvisos] = useState<Aviso[]>([]);

  const remover = useCallback((id: number) => {
    setAvisos((a) => a.map((x) => (x.id === id ? { ...x, saindo: true } : x)));
    setTimeout(() => setAvisos((a) => a.filter((x) => x.id !== id)), SAIDA_MS);
  }, []);

  const adicionar = useCallback(
    (tipo: Aviso["tipo"], msg: string) => {
      const id = ++proximoId;
      setAvisos((a) => [...a, { id, tipo, msg }]);
      // Erro fica até ser fechado: quem lê devagar ou usa leitor de tela não perde a mensagem (WCAG 2.2.1).
      if (tipo === "sucesso") setTimeout(() => remover(id), DURACAO_MS);
    },
    [remover],
  );

  const valor = useMemo<ContextoToast>(() => ({ sucesso: (m) => adicionar("sucesso", m), erro: (m) => adicionar("erro", m) }), [adicionar]);

  return (
    <Contexto value={valor}>
      {children}
      <div className="fixed right-4 bottom-24 z-50 md:bottom-4 flex max-w-sm flex-col gap-2">
        {avisos
          .filter((a) => a.tipo === "erro")
          .map((a) => (
            // `urgente` já vem com role="alert": o erro é anunciado na hora.
            <Alert key={a.id} variante="urgente" className={cn("flex items-start gap-2 pr-1.5 shadow-lg", a.saindo ? "animate-sumir" : "animate-surgir")}>
              <span className="flex-1 py-0.5">{a.msg}</span>
              <button
                type="button"
                aria-label="Fechar aviso"
                onClick={() => remover(a.id)}
                className="grid size-7 flex-none cursor-pointer place-items-center rounded-md transition-colors duration-150 hover:bg-urgente/10 focus-visible:outline-2 focus-visible:outline-urgente pointer-coarse:size-11"
              >
                <X aria-hidden="true" className="size-4" />
              </button>
            </Alert>
          ))}
        {/* Região viva sempre montada: o leitor de tela só anuncia o que entra numa região que já existia. */}
        <div role="status" className="flex flex-col gap-2">
          {avisos
            .filter((a) => a.tipo === "sucesso")
            .map((a) => (
              <Alert key={a.id} variante="realizado" className={cn("shadow-lg", a.saindo ? "animate-sumir" : "animate-surgir")}>
                {a.msg}
              </Alert>
            ))}
        </div>
      </div>
    </Contexto>
  );
}

export function useToast(): ContextoToast {
  const ctx = useContext(Contexto);
  if (!ctx) throw new Error("useToast requer ToastProvider");
  return ctx;
}
