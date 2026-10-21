import type { ReactNode } from "react";
import { AcessoNegado } from "../components/layout/AcessoNegado";
import { pode, type Capacidade } from "./capacidades";
import { useSessao } from "./useSessao";

export function RotaPermitida({ capacidade, children }: { capacidade: Capacidade; children: ReactNode }) {
  const { usuario } = useSessao();
  return pode(usuario, capacidade) ? children : <AcessoNegado />;
}
