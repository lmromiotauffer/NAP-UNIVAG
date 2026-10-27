/** Só caminhos relativos da própria origem: bloqueia "//host", "/\host" e caracteres de controle (open redirect). */
export function destinoSeguro(voltar: string | null | undefined, padrao = "/"): string {
  if (!voltar || !/^\/(?![/\\])/.test(voltar) || /[\u0000-\u001f\\]/.test(voltar) || voltar.startsWith("/entrar")) return padrao;
  return voltar;
}
