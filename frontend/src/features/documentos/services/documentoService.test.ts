import { afterEach, describe, expect, it, vi } from "vitest";
import * as baixar from "../../../lib/baixarArquivo";
import { documentoService } from "./documentoService";

afterEach(() => vi.restoreAllMocks());

describe("documentoService.baixar", () => {
  it("Documento_arquivado_baixa_com_incluirArquivados", async () => {
    const espiao = vi.spyOn(baixar, "baixarArquivo").mockResolvedValue();
    await documentoService.baixar({ id: "d1", arquivado: true });
    expect(espiao).toHaveBeenCalledWith("/documentos/d1/arquivo?incluirArquivados=true");
  });

  it("Documento_ativo_baixa_sem_o_parametro", async () => {
    const espiao = vi.spyOn(baixar, "baixarArquivo").mockResolvedValue();
    await documentoService.baixar({ id: "d1", arquivado: false });
    expect(espiao).toHaveBeenCalledWith("/documentos/d1/arquivo");
  });
});
