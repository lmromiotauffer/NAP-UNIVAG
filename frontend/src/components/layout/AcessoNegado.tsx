import { Lock } from "lucide-react";
import { Link } from "react-router";
import { EmptyState } from "../ui/EmptyState";

export function AcessoNegado() {
  return (
    <>
      <title>Acesso negado · NAP</title>
      <EmptyState
        nivel="h1"
        icone={<Lock />}
        titulo="Acesso negado"
        descricao="Seu perfil não tem permissão para acessar esta área. Se precisar dela, fale com a coordenação do NAP."
        acao={
          <Link to="/" className="text-sm font-semibold text-primaria underline-offset-2 hover:underline">
            Voltar ao painel
          </Link>
        }
      />
    </>
  );
}
