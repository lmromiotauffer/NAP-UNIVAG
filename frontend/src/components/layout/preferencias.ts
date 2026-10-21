// localStorage só guarda preferências de interface (nunca token); pode estar bloqueado, daí o try/catch.
function lerPreferencia(chave: string, padrao: string): string {
  try {
    return localStorage.getItem(chave) ?? padrao;
  } catch {
    return padrao;
  }
}

export function salvarPreferencia(chave: string, valor: string): void {
  try {
    localStorage.setItem(chave, valor);
  } catch {
    // Sem armazenamento: a preferência vale só nesta visita.
  }
}

export type Tema = "claro" | "escuro";

function temaDoSistema(): Tema {
  try {
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "escuro" : "claro";
  } catch {
    return "claro";
  }
}

export function temaAtual(): Tema {
  const salvo = lerPreferencia("nap.tema", temaDoSistema());
  return salvo === "escuro" ? "escuro" : "claro";
}

// O tema vive em <html data-theme>; os botões (trilho e barra inferior) leem dali, então ficam sempre em acordo.
const ouvintes = new Set<() => void>();

export function assinarTema(ouvinte: () => void): () => void {
  ouvintes.add(ouvinte);
  return () => ouvintes.delete(ouvinte);
}

export function temaAplicado(): Tema {
  return document.documentElement.dataset.theme === "escuro" ? "escuro" : "claro";
}

export function aplicarTema(tema: Tema): void {
  document.documentElement.dataset.theme = tema;
  ouvintes.forEach((o) => o());
}

/** Enquanto a pessoa não escolheu um tema, acompanha a troca de claro/escuro do sistema. */
export function acompanharSistema(): void {
  try {
    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", (e) => {
      if (lerPreferencia("nap.tema", "") === "") aplicarTema(e.matches ? "escuro" : "claro");
    });
  } catch {
    // Sem matchMedia: fica o tema inicial.
  }
}
