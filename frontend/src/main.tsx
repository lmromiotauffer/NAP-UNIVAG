import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./styles/index.css";
import App from "./App";
import { acompanharSistema, aplicarTema, temaAtual } from "./components/layout/preferencias";

// O script inline do index.html já aplicou o tema antes do CSS; aqui só confirma (e cobre ambientes sem ele).
aplicarTema(temaAtual());
acompanharSistema();

// Chunk que sumiu após um deploy: recarrega uma vez para buscar o index novo. Outra falha em menos de 10 s vai para o
// errorElement (sem laço de recarga). Sem sessionStorage (modo privado, bloqueado), não recarrega sozinho.
window.addEventListener("vite:preloadError", (evento) => {
  try {
    const chave = "nap:recarga-chunk";
    if (Date.now() - Number(sessionStorage.getItem(chave) ?? 0) < 10_000) return;
    sessionStorage.setItem(chave, String(Date.now()));
  } catch {
    return;
  }
  evento.preventDefault();
  window.location.reload();
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
