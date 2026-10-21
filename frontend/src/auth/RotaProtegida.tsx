import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router";
import { Carregando } from "../components/ui/Carregando";
import { Skeleton } from "../components/ui/Skeleton";
import { useSessao } from "./useSessao";

export function RotaProtegida({ children }: { children: ReactNode }) {
  const { estado, usuario, saiuPorVontade } = useSessao();
  const { pathname, search } = useLocation();

  if (estado === "carregando") {
    return (
      <Carregando rotulo="Carregando sessão" className="flex min-h-dvh bg-fundo">
        <Skeleton className="hidden h-dvh w-28 rounded-none md:block" />
        <div className="flex flex-1 flex-col gap-4 p-6">
          <Skeleton className="h-8 w-72" />
          <Skeleton className="h-28 w-full" />
          <Skeleton className="h-64 w-full" />
        </div>
      </Carregando>
    );
  }
  if (estado === "anonimo") {
    // Sessão expirada ou ausente: volta para cá depois do login.
    const voltar = pathname + search;
    return <Navigate to={saiuPorVontade || voltar === "/" ? "/entrar" : `/entrar?voltar=${encodeURIComponent(voltar)}`} replace />;
  }
  // Troca pendente: o backend responde 403 em todo o resto, então nada mais é acessível.
  if (usuario?.deveTrocarSenha && pathname !== "/definir-senha") return <Navigate to="/definir-senha" replace />;
  return children;
}
