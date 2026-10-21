import { extendTailwindMerge } from "tailwind-merge";

// Ensina ao tailwind-merge os tamanhos e raios de tokens.css; sem isso `text-denso` vira "cor" e apaga `text-suave`.
export const cn = extendTailwindMerge({
  extend: {
    theme: {
      text: ["micro", "mini", "rotulo", "denso", "corpo", "destaque", "titulo-pagina"],
      radius: ["campo", "bloco", "cartao", "secao"],
    },
  },
});
