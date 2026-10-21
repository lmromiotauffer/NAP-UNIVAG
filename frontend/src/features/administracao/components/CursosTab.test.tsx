// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../../components/ui/Toast";
import { responderApi } from "../../../test/apiFalsa";
import { administracaoService } from "../services/administracaoService";
import { CursosTab } from "./CursosTab";

vi.mock("../services/administracaoService", () => ({
  administracaoService: { situacaoCurso: vi.fn().mockResolvedValue(undefined), renomearCurso: vi.fn(), criarCurso: vi.fn() },
}));

responderApi({
  "/cursos": [
    { id: "c1", nome: "Direito", ativo: true, versao: "v1" },
    { id: "c2", nome: "Psicologia", ativo: false, versao: "v7" },
  ],
});

function montar() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <CursosTab />
      </ToastProvider>
    </QueryClientProvider>,
  );
  return userEvent.setup();
}

describe("CursosTab", () => {
  it("Curso_sem_inativar_mostra_reativar", async () => {
    montar();
    expect(await screen.findByRole("button", { name: "Inativar Direito" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reativar Psicologia" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Inativar Psicologia" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reativar Direito" })).not.toBeInTheDocument();
  });

  it("Reativar_envia_ativo_true_com_a_versao_lida", async () => {
    const user = montar();
    await user.click(await screen.findByRole("button", { name: "Reativar Psicologia" }));
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Reativar" }));
    expect(administracaoService.situacaoCurso).toHaveBeenCalledWith("c2", { ativo: true }, "v7");
  });
});
