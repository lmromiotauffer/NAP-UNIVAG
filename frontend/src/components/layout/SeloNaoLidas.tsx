/** Selo numérico sobre o ícone; o nome acessível traz o total real mesmo quando o texto visível é "99+". */
export function SeloNaoLidas({ total }: { total: number }) {
  if (total <= 0) return null;
  return (
    <span className="animate-surgir absolute -top-1.5 -right-2.5 grid min-w-[1.125rem] place-items-center rounded-full bg-urgente px-1 text-micro leading-[1.125rem] font-bold text-primaria-texto">
      <span aria-hidden="true">{total > 99 ? "99+" : total}</span>
      <span className="sr-only">{total} não lidas</span>
    </span>
  );
}
