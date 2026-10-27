/** Selo numérico sobre o ícone; o nome acessível traz o total real mesmo quando o texto visível é "99+". */
export function SeloNaoLidas({ total }: { total: number }) {
  if (total <= 0) return null;
  return (
    // `key={total}`: a cada mudança do total o selo remonta e chega de novo (subida curta), sinalizando que algo novo entrou.
    // O --entrada-y local venceria o zero do :root em prefers-reduced-motion: por isso o motion-reduce explícito.
    <span key={total} className="animate-surgir [--entrada-y:0.25rem] motion-reduce:[--entrada-y:0] absolute -top-1.5 -right-2.5 grid min-w-[1.125rem] place-items-center rounded-full bg-urgente px-1 text-micro leading-[1.125rem] font-bold text-primaria-texto">
      <span aria-hidden="true">{total > 99 ? "99+" : total}</span>
      <span className="sr-only">{total} não lidas</span>
    </span>
  );
}
