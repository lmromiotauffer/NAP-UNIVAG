import { SearchX } from "lucide-react";
import { Link } from "react-router";
import { EmptyState } from "../ui/EmptyState";

export function PaginaNaoEncontrada() {
  return (
    <>
      <title>Página não encontrada · NAP</title>
      <EmptyState
        nivel="h1"
        icone={<SearchX />}
        titulo="Página não encontrada"
        descricao="O endereço não existe ou foi movido. Confira o link ou use o menu."
        acao={
          <Link to="/" className="text-sm font-semibold text-primaria underline-offset-2 hover:underline">
            Ir para o painel
          </Link>
        }
      />
    </>
  );
}
