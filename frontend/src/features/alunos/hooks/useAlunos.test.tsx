// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { alunoService } from "../services/alunoService";
import { useAtualizarAluno } from "./useAlunos";

vi.mock("../services/alunoService", () => ({ alunoService: { atualizar: vi.fn() } }));

describe("useAtualizarAluno", () => {
  it("conflito_na_edicao_revalida_a_ficha", async () => {
    vi.mocked(alunoService.atualizar).mockRejectedValueOnce({ status: 409 });
    const cliente = new QueryClient();
    const invalidar = vi.spyOn(cliente, "invalidateQueries");
    const { result } = renderHook(() => useAtualizarAluno(), {
      wrapper: ({ children }: { children: ReactNode }) => <QueryClientProvider client={cliente}>{children}</QueryClientProvider>,
    });

    await expect(result.current.mutateAsync({ id: "a1", body: {} as never, versao: "v1" })).rejects.toMatchObject({ status: 409 });

    expect(invalidar).toHaveBeenCalled();
    expect(alunoService.atualizar).toHaveBeenCalledWith("a1", expect.any(Object), "v1");
  });
});
