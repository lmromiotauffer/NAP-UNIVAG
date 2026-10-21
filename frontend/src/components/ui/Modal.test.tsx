// @vitest-environment jsdom
import { MutationObserver, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState, type ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { ConfirmDialog } from "./ConfirmDialog";
import { Modal } from "./Modal";

function Exemplo() {
  const [aberto, setAberto] = useState(false);
  return (
    <>
      <button onClick={() => setAberto(true)}>Abrir</button>
      <Modal aberto={aberto} aoFechar={() => setAberto(false)} titulo="Título">
        <button>Dentro</button>
        <textarea aria-label="Notas" />
      </Modal>
    </>
  );
}

// O Modal lê envios em voo do QueryClient (useIsMutating).
function renderizar(ui: ReactElement, cliente = new QueryClient()) {
  return render(<QueryClientProvider client={cliente}>{ui}</QueryClientProvider>);
}

describe("Modal", () => {
  it("modal_restaura_foco_ao_fechar", async () => {
    const u = userEvent.setup();
    renderizar(<Exemplo />);
    const abrir = screen.getByRole("button", { name: "Abrir" });
    await u.click(abrir);
    expect(await screen.findByRole("button", { name: "Fechar" })).toBeInTheDocument();
    await u.click(screen.getByRole("button", { name: "Fechar" }));
    expect(abrir).toHaveFocus();
  });

  it("modal_fecha_com_esc", async () => {
    const u = userEvent.setup();
    renderizar(<Exemplo />);
    await u.click(screen.getByRole("button", { name: "Abrir" }));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    // o jsdom não dispara "cancel" com Esc; o Modal trata keydown Escape além do evento nativo.
    await u.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("confirm_dialog_carregando_ignora_esc_backdrop_e_x", async () => {
    const u = userEvent.setup();
    const aoCancelar = vi.fn();
    renderizar(<ConfirmDialog aberto carregando titulo="T" mensagem="M" confirmar="Sim" aoConfirmar={() => {}} aoCancelar={aoCancelar} />);
    expect(screen.getByRole("button", { name: "Fechar" })).toBeDisabled();
    await u.keyboard("{Escape}");
    await u.click(screen.getByRole("dialog", { hidden: true }));
    expect(aoCancelar).not.toHaveBeenCalled();
  });

  it("modal_nao_fecha_ao_arrastar_selecao_de_um_campo_ate_o_fundo", async () => {
    const u = userEvent.setup();
    renderizar(<Exemplo />);
    await u.click(screen.getByRole("button", { name: "Abrir" }));
    const dialogo = screen.getByRole("dialog");
    fireEvent.pointerDown(screen.getByLabelText("Notas"));
    fireEvent.click(dialogo);
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    fireEvent.pointerDown(dialogo);
    fireEvent.click(dialogo);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("modal_nao_fecha_com_envio_em_voo", async () => {
    const u = userEvent.setup();
    const cliente = new QueryClient();
    renderizar(<Exemplo />, cliente);
    await u.click(screen.getByRole("button", { name: "Abrir" }));
    void new MutationObserver(cliente, { mutationFn: () => new Promise(() => {}) }).mutate();
    await waitFor(() => expect(screen.getByRole("button", { name: "Fechar" })).toBeDisabled());
    await u.keyboard("{Escape}");
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
});
