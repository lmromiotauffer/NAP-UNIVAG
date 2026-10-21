import { lazy } from "react";
import { createBrowserRouter, Navigate, type RouteObject } from "react-router";
import { RotaPermitida } from "./auth/RotaPermitida";
import { RotaProtegida } from "./auth/RotaProtegida";
import { AppShell } from "./components/layout/AppShell";
import { ErroRota } from "./components/layout/ErroRota";
import { PaginaNaoEncontrada } from "./components/layout/PaginaNaoEncontrada";

// Uma página por chunk: quem abre o link de assinatura ou o login não baixa o sistema inteiro.
const LoginPage = lazy(() => import("./auth/LoginPage").then((m) => ({ default: m.LoginPage })));
const TrocarSenhaPage = lazy(() => import("./auth/TrocarSenhaPage").then((m) => ({ default: m.TrocarSenhaPage })));
const AgendaPage = lazy(() => import("./features/agenda/AgendaPage").then((m) => ({ default: m.AgendaPage })));
const AdministracaoPage = lazy(() => import("./features/administracao/AdministracaoPage").then((m) => ({ default: m.AdministracaoPage })));
const AlunosPage = lazy(() => import("./features/alunos/AlunosPage").then((m) => ({ default: m.AlunosPage })));
const FichaAlunoPage = lazy(() => import("./features/alunos/FichaAlunoPage").then((m) => ({ default: m.FichaAlunoPage })));
const AssinarPeiPage = lazy(() => import("./features/assinatura/AssinarPeiPage").then((m) => ({ default: m.AssinarPeiPage })));
const AtendimentosPage = lazy(() => import("./features/atendimentos/AtendimentosPage").then((m) => ({ default: m.AtendimentosPage })));
const AtividadesPage = lazy(() => import("./features/atividades/AtividadesPage").then((m) => ({ default: m.AtividadesPage })));
const ComunicacoesPage = lazy(() => import("./features/comunicacoes/ComunicacoesPage").then((m) => ({ default: m.ComunicacoesPage })));
const DetalheMensagemPage = lazy(() => import("./features/comunicacoes/DetalheMensagemPage").then((m) => ({ default: m.DetalheMensagemPage })));
const DetalheEncaminhamentoPage = lazy(() => import("./features/encaminhamentos/DetalheEncaminhamentoPage").then((m) => ({ default: m.DetalheEncaminhamentoPage })));
const EncaminhamentosPage = lazy(() => import("./features/encaminhamentos/EncaminhamentosPage").then((m) => ({ default: m.EncaminhamentosPage })));
const PainelPage = lazy(() => import("./features/painel/PainelPage").then((m) => ({ default: m.PainelPage })));
const ProducaoPage = lazy(() => import("./features/producao/ProducaoPage").then((m) => ({ default: m.ProducaoPage })));
const VersaoPeiPage = lazy(() => import("./features/peis/VersaoPeiPage").then((m) => ({ default: m.VersaoPeiPage })));

const rotasDoSistema: RouteObject[] = [
  { path: "/entrar", element: <LoginPage /> },
  // Fora da sessão e do AppShell: o aluno assina por link temporário, sem login.
  { path: "/assinar/:token", element: <AssinarPeiPage /> },
  // Com sessão, mas fora do AppShell: com troca pendente é a única tela acessível.
  { path: "/definir-senha", element: <RotaProtegida><TrocarSenhaPage /></RotaProtegida> },
  {
    element: (
      <RotaProtegida>
        <AppShell />
      </RotaProtegida>
    ),
    // Rota sem caminho entre o AppShell e as páginas: o erro de uma página aparece no <Outlet>, com o menu.
    children: [{ errorElement: <ErroRota />, children: [
      { index: true, element: <RotaPermitida capacidade="painel"><PainelPage /></RotaPermitida> },
      { path: "alunos", element: <RotaPermitida capacidade="alunos.consultar"><AlunosPage /></RotaPermitida> },
      { path: "alunos/:id", element: <RotaPermitida capacidade="alunos.consultar"><FichaAlunoPage /></RotaPermitida> },
      { path: "agenda", element: <RotaPermitida capacidade="agenda"><AgendaPage /></RotaPermitida> },
      { path: "atendimentos", element: <RotaPermitida capacidade="atendimentos.registrar"><AtendimentosPage /></RotaPermitida> },
      { path: "atividades", element: <RotaPermitida capacidade="atividades"><AtividadesPage /></RotaPermitida> },
      { path: "producao", element: <RotaPermitida capacidade="producao"><ProducaoPage /></RotaPermitida> },
      { path: "encaminhamentos", element: <RotaPermitida capacidade="encaminhamentos"><EncaminhamentosPage /></RotaPermitida> },
      { path: "encaminhamentos/:id", element: <RotaPermitida capacidade="encaminhamentos"><DetalheEncaminhamentoPage /></RotaPermitida> },
      { path: "peis/versoes/:id", element: <RotaPermitida capacidade="peis"><VersaoPeiPage /></RotaPermitida> },
      { path: "comunicacoes", element: <RotaPermitida capacidade="comunicacoes"><ComunicacoesPage /></RotaPermitida> },
      { path: "comunicacoes/:id", element: <RotaPermitida capacidade="comunicacoes"><DetalheMensagemPage /></RotaPermitida> },
      { path: "administracao", element: <RotaPermitida capacidade="administracao"><AdministracaoPage /></RotaPermitida> },
      // Notificações virou aba de Comunicações: links antigos (e-mails, favoritos) continuam chegando lá.
      { path: "notificacoes", element: <Navigate to="/comunicacoes?aba=notificacoes" replace /> },
      { path: "*", element: <PaginaNaoEncontrada /> },
    ] }],
  },
];

// Raiz sem caminho: o que escapar das páginas (ex.: erro no próprio AppShell) cai aqui, nunca na tela padrão do react-router.
export const rotas: RouteObject[] = [{ errorElement: <ErroRota telaCheia />, children: rotasDoSistema }];

export const router = createBrowserRouter(rotas);
