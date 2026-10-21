import type { FaixaEscala } from "./types";

const FUSO = "America/Cuiaba";
const LOCALE = "pt-BR";
const AUSENTE = "—";

const MESES = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

/** Data civil no formato yyyy-MM-dd (só a forma; o valor é conferido por quem usa). */
export const DIA = /^\d{4}-\d{2}-\d{2}$/;
/** Horário "HH:mm", de 00:00 a 23:59. */
export const HORA = /^([01]\d|2[0-3]):[0-5]\d$/;

type Ausente = null | undefined;

function partes(iso: string): Record<string, string> {
  const fmt = new Intl.DateTimeFormat(LOCALE, {
    timeZone: FUSO,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  });
  const resultado: Record<string, string> = {};
  for (const p of fmt.formatToParts(new Date(iso))) resultado[p.type] = p.value;
  return resultado;
}

export function diaCivil(iso: string): string {
  const p = partes(iso);
  return `${p.year}-${p.month}-${p.day}`;
}

export function formatarData(iso: string | Ausente): string {
  if (iso == null) return AUSENTE;
  const p = partes(iso);
  return `${p.day}/${p.month}/${p.year}`;
}

/** Campo só de data ("yyyy-MM-dd"): sem passar por Date, que leria meia-noite UTC e voltaria um dia em Cuiabá. */
export function formatarDataCivil(dia: string | Ausente): string {
  if (dia == null) return AUSENTE;
  const [ano, mes, d] = dia.split("-");
  return `${d}/${mes}/${ano}`;
}

export function formatarHora(iso: string | Ausente): string {
  if (iso == null) return AUSENTE;
  const p = partes(iso);
  return `${Number(p.hour)}h${p.minute}`;
}

export function formatarDataHora(iso: string | Ausente): string {
  if (iso == null) return AUSENTE;
  return `${formatarData(iso)} ${formatarHora(iso)}`;
}

/** "out" para mês 10 (1–12), sem passar por Date. */
export function formatarMesCurto(mes: number): string {
  return MESES[mes - 1] ?? AUSENTE;
}

export function formatarDuracao(minutos: number | Ausente): string {
  if (minutos == null) return AUSENTE;
  if (minutos < 60) return `${minutos} min`;
  const horas = Math.floor(minutos / 60);
  return `${formatarInteiro(horas)}h${String(minutos % 60).padStart(2, "0")}`;
}

export function formatarInteiro(valor: number | Ausente): string {
  if (valor == null) return AUSENTE;
  return new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 0 }).format(valor);
}

export function formatarPercentual(fracao: number | Ausente, casas = 0): string {
  if (fracao == null) return AUSENTE;
  return new Intl.NumberFormat(LOCALE, {
    style: "percent",
    // Variação que arredonda a zero sai "0%", nunca "-0%".
    signDisplay: "negative",
    minimumFractionDigits: casas,
    maximumFractionDigits: casas,
  }).format(fracao);
}

/** Pela hora local de Cuiabá: antes das 12h "Bom dia", das 12h às 17h59 "Boa tarde", depois "Boa noite". */
export function saudacao(data: Date = new Date()): string {
  const hora = Number(new Intl.DateTimeFormat(LOCALE, { timeZone: FUSO, hour: "numeric", hourCycle: "h23" }).format(data));
  return hora < 12 ? "Bom dia" : hora < 18 ? "Boa tarde" : "Boa noite";
}

/** "quinta, 1 de outubro de 2026" (sem o "-feira", como no protótipo). */
export function formatarDataLonga(data: Date = new Date()): string {
  return new Intl.DateTimeFormat(LOCALE, { timeZone: FUSO, weekday: "long", day: "numeric", month: "long", year: "numeric" })
    .format(data)
    .replace("-feira", "");
}

// Agenda: dias civis "yyyy-MM-dd" são calculados em UTC puro, sem depender do fuso do navegador.
const utc = (dia: string): Date => new Date(`${dia}T00:00:00Z`);

// Segunda a domingo, a ordem em que a escala é preenchida e lida.
export const DIAS_ESCALA: { dia: FaixaEscala["dia"]; nome: string; curto: string }[] = [
  { dia: "Monday", nome: "Segunda-feira", curto: "Seg" },
  { dia: "Tuesday", nome: "Terça-feira", curto: "Ter" },
  { dia: "Wednesday", nome: "Quarta-feira", curto: "Qua" },
  { dia: "Thursday", nome: "Quinta-feira", curto: "Qui" },
  { dia: "Friday", nome: "Sexta-feira", curto: "Sex" },
  { dia: "Saturday", nome: "Sábado", curto: "Sáb" },
  { dia: "Sunday", nome: "Domingo", curto: "Dom" },
];

/** Dia da semana de um dia civil "yyyy-MM-dd". */
export const diaDaSemana = (dia: string) => DIAS_ESCALA[(utc(dia).getUTCDay() + 6) % 7]!;

/** Dia civil de hoje em Cuiabá. */
export function hojeCivil(agora: Date = new Date()): string {
  return diaCivil(agora.toISOString());
}

export function somarDias(dia: string, n: number): string {
  const d = utc(dia);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}

/** Segunda-feira da semana de um dia civil (o calendário institucional, não o do navegador). */
export function segundaDoDia(dia: string): string {
  return somarDias(dia, -((utc(dia).getUTCDay() + 6) % 7));
}

/** "HH:mm" em Cuiabá. */
export function horaCivil(iso: string): string {
  const p = partes(iso);
  return `${p.hour}:${p.minute}`;
}

/** Dia civil + "HH:mm" de Cuiabá → ISO com o deslocamento do fuso naquele dia (ex.: "-04:00"). */
export function instanteCivil(dia: string, hora: string): string {
  const nome = new Intl.DateTimeFormat("en-US", { timeZone: FUSO, timeZoneName: "longOffset" })
    .formatToParts(new Date(`${dia}T12:00:00Z`))
    .find((p) => p.type === "timeZoneName")?.value;
  const deslocamento = nome === "GMT" || !nome ? "+00:00" : nome.slice(3);
  return `${dia}T${hora}:00${deslocamento}`;
}

/** "28 set – 2 out 2026" (segunda a sexta). */
export function formatarSemana(segunda: string): string {
  const sexta = somarDias(segunda, 4);
  const [a1, m1, d1] = segunda.split("-").map(Number) as [number, number, number];
  const [a2, m2, d2] = sexta.split("-").map(Number) as [number, number, number];
  const fim = `${d2} ${MESES[m2 - 1]} ${a2}`;
  if (a1 !== a2) return `${d1} ${MESES[m1 - 1]} ${a1} – ${fim}`;
  if (m1 !== m2) return `${d1} ${MESES[m1 - 1]} – ${fim}`;
  return `${d1} – ${fim}`;
}

/** "Seg 28". */
export function formatarDiaSemana(dia: string): string {
  return `${diaDaSemana(dia).curto} ${Number(dia.slice(8))}`;
}

/** Tamanho em unidades de 1024 (o limite de upload é 5 MiB): "512 B", "1,2 MB". */
export function formatarTamanho(bytes: number | Ausente): string {
  if (bytes == null) return AUSENTE;
  if (bytes < 1024) return `${bytes} B`;
  const unidades = ["KB", "MB", "GB"];
  let valor = bytes / 1024;
  let i = 0;
  while (valor >= 1024 && i < unidades.length - 1) {
    valor /= 1024;
    i++;
  }
  return `${new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 1 }).format(valor)} ${unidades[i]}`;
}
