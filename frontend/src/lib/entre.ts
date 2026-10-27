/** O valor tipado se está na lista (enum vindo da URL ou de um select); senão undefined. */
export const entre = <T extends string>(lista: readonly T[], v: string | null | undefined): T | undefined => lista.find((x) => x === v);
