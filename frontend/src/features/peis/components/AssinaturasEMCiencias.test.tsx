// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { versaoBase } from "../../../test/peis";
import { AssinaturasEMCiencias } from "./AssinaturasEMCiencias";

describe("AssinaturasEMCiencias", () => {
  it("Aviso_de_assinatura_nao_certificada_aparece", () => {
    render(<AssinaturasEMCiencias versao={{ ...versaoBase, assinaturas: [{ papel: "Profissional", nome: "Ana Beatriz", em: "2026-03-01T10:00:00-04:00", metodo: "Demonstrativa" }] }} />);
    expect(screen.getByText("Assinatura demonstrativa — não certificada (pendência institucional)")).toBeInTheDocument();
  });

  it("Ciencia_registrada_aparece_na_versao_publicada", () => {
    render(<AssinaturasEMCiencias versao={{ ...versaoBase, estado: "Publicada", ciencias: [{ coordenador: "Carlos Direito", em: "2026-03-02T09:00:00-04:00" }] }} />);
    expect(screen.getByText(/Ciência registrada em/)).toBeInTheDocument();
  });
});
