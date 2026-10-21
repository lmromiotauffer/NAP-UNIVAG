import { Settings } from "lucide-react";
import { useSearchParams } from "react-router";
import { pode } from "../../auth/capacidades";
import { useSessao } from "../../auth/useSessao";
import { classeIconeTitulo, classeTituloPagina } from "../../components/ui/Card";
import { ListaAbas } from "../../components/ui/ListaAbas";
import { cn } from "../../lib/cn";
import { AlertasTab } from "./components/AlertasTab";
import { AuditoriaTab } from "./components/AuditoriaTab";
import { CursosTab } from "./components/CursosTab";
import { EscalasTab } from "./components/EscalasTab";
import { FeriadosTab } from "./components/FeriadosTab";
import { UsuariosTab } from "./components/UsuariosTab";

type Aba = "usuarios" | "cursos" | "escalas" | "feriados" | "alertas" | "auditoria";

const TODAS_ABAS: { chave: Aba; rotulo: string }[] = [
  { chave: "usuarios", rotulo: "Usuários" },
  { chave: "cursos", rotulo: "Cursos" },
  { chave: "escalas", rotulo: "Escalas" },
  { chave: "feriados", rotulo: "Feriados" },
  { chave: "alertas", rotulo: "Alertas" },
  { chave: "auditoria", rotulo: "Auditoria" },
];

export function AdministracaoPage() {
  const { usuario } = useSessao();
  const [params, setParams] = useSearchParams();
  const ABAS = TODAS_ABAS.filter((a) => a.chave !== "auditoria" || pode(usuario, "administracao"));
  const aba = ABAS.find((a) => a.chave === params.get("aba")) ?? ABAS[0]!;

  // Trocar de aba descarta busca, página e filtros da aba anterior.
  const trocarAba = (chave: Aba) => setParams(chave === "usuarios" ? {} : { aba: chave });

  return (
    <>
      <title>Administração · NAP</title>
      <header>
        <h1 className={cn(classeTituloPagina, "flex items-center gap-3")}>
          <span aria-hidden="true" className={classeIconeTitulo}>
            <Settings className="size-5" />
          </span>
          Administração
        </h1>
      </header>

      <ListaAbas abas={ABAS} atual={aba.chave} aoTrocar={trocarAba} rotulo="Áreas da administração" />
      {/* `key`: o painel remonta a cada troca de aba e chega com o animate-surgir que o <main> aplica aos filhos diretos. */}
      <div key={aba.chave} role="tabpanel" id={`painel-${aba.chave}`} aria-labelledby={`aba-${aba.chave}`} className="flex flex-col gap-[18px] md:min-h-0 md:flex-1">
        {aba.chave === "usuarios" && <UsuariosTab />}
        {aba.chave === "cursos" && <CursosTab />}
        {aba.chave === "escalas" && <EscalasTab />}
        {aba.chave === "feriados" && <FeriadosTab />}
        {aba.chave === "alertas" && <AlertasTab />}
        {aba.chave === "auditoria" && <AuditoriaTab />}
      </div>
    </>
  );
}
