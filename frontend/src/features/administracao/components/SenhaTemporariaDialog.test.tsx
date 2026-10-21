// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render as renderizar, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { SenhaTemporariaDialog } from "./SenhaTemporariaDialog";

const render = (ui: ReactElement) => renderizar(<QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>);

describe("SenhaTemporariaDialog", () => {
  it("mostra_e_mail_senha_e_o_aviso_de_entrega", () => {
    render(<SenhaTemporariaDialog email="ana@nap.demo" senha="Kq7mTzP4wXn9HbVd" aoFechar={vi.fn()} />);
    expect(screen.getByText("ana@nap.demo")).toBeInTheDocument();
    expect(screen.getByText("Kq7mTzP4wXn9HbVd")).toBeInTheDocument();
    expect(screen.getByText("Entregue esta senha pessoalmente. Ela aparece só agora e precisará ser trocada no primeiro acesso.")).toBeInTheDocument();
  });

  it("copiar_envia_a_senha_para_a_area_de_transferencia", async () => {
    const user = userEvent.setup();
    const escrever = vi.spyOn(navigator.clipboard, "writeText").mockResolvedValue();
    render(<SenhaTemporariaDialog email="ana@nap.demo" senha="Kq7mTzP4wXn9HbVd" aoFechar={vi.fn()} />);
    await user.click(screen.getByRole("button", { name: "Copiar" }));
    expect(escrever).toHaveBeenCalledWith("Kq7mTzP4wXn9HbVd");
    expect(await screen.findByRole("button", { name: "Copiada" })).toBeInTheDocument();
  });

  it("sem_senha_nada_e_exibido", () => {
    render(<SenhaTemporariaDialog email="ana@nap.demo" senha={null} aoFechar={vi.fn()} />);
    expect(screen.queryByText("ana@nap.demo")).not.toBeInTheDocument();
  });
});
