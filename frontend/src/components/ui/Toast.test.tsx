// @vitest-environment jsdom
import { act, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ToastProvider, useToast } from "./Toast";

let toast: ReturnType<typeof useToast>;
function Captura() {
  toast = useToast();
  return null;
}

describe("Toast", () => {
  afterEach(() => vi.useRealTimers());

  it("erro_fica_ate_fechar_e_sucesso_some_sozinho", () => {
    vi.useFakeTimers();
    render(
      <ToastProvider>
        <Captura />
      </ToastProvider>,
    );
    act(() => {
      toast.sucesso("Salvo.");
      toast.erro("Falhou.");
    });
    expect(screen.getByRole("alert")).toHaveTextContent("Falhou.");
    expect(screen.getByRole("status")).toHaveTextContent("Salvo.");

    act(() => vi.advanceTimersByTime(10_000));
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
    expect(screen.getByRole("alert")).toHaveTextContent("Falhou.");

    act(() => screen.getByRole("button", { name: "Fechar aviso" }).click());
    // Esmaece por 150 ms antes de desmontar.
    expect(screen.getByRole("alert")).toHaveClass("animate-sumir");
    act(() => vi.advanceTimersByTime(150));
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
