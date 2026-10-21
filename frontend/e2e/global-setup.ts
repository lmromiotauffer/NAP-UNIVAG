/*
 * Sobe uma API isolada para as jornadas: banco NapE2E recriado a cada execução, Redis no banco lógico 1,
 * senha de seed e chave JWT aleatórias. Nada vem dos user-secrets do desenvolvedor.
 * O Vite sobe pelo `webServer` do playwright.config.ts, com proxy para esta API.
 */
import { spawn, spawnSync, type ChildProcess } from "node:child_process";
import { randomBytes } from "node:crypto";
import { closeSync, mkdtempSync, openSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { parseEnv } from "node:util";
import { request } from "@playwright/test";

const RAIZ = resolve(import.meta.dirname, "../..");
const API = "https://localhost:7180";
const PRAZO_API_MS = 4 * 60_000;
const LOG_API = join(tmpdir(), "nap-api-e2e.log");

function docker(servico: string, comando: string) {
  const r = spawnSync("docker", ["compose", "exec", "-T", servico, "sh", "-c", comando], { cwd: RAIZ, encoding: "utf8" });
  if (r.status !== 0) throw new Error(`Falha no docker compose exec ${servico}: ${r.stderr || r.error?.message}`);
}

const COMO_LIBERAR_PORTA =
  process.platform === "win32"
    ? "Ache o processo com `netstat -ano | findstr :7180` e encerre-o com `taskkill /PID <pid> /T /F`."
    : "Ache o processo com `lsof -i :7180` e encerre-o com `kill <pid>`.";

function encerrar(processo: ChildProcess) {
  if (processo.pid === undefined || processo.exitCode !== null) return;
  // `dotnet run` abre um processo filho (Nap.Api): só a árvore inteira libera a porta 7180.
  if (process.platform === "win32") {
    const r = spawnSync("taskkill", ["/PID", String(processo.pid), "/T", "/F"], { encoding: "utf8" });
    if (r.status !== 0) console.warn(`[e2e] Falha ao encerrar a API (taskkill ${r.status}): ${r.stderr || r.error?.message}. ${COMO_LIBERAR_PORTA}`);
  } else process.kill(-processo.pid, "SIGTERM");
}

async function apiResponde(caminho: string): Promise<boolean> {
  const contexto = await request.newContext({ ignoreHTTPSErrors: true });
  try {
    return (await contexto.get(`${API}${caminho}`, { timeout: 2_000 })).ok();
  } catch {
    return false;
  } finally {
    await contexto.dispose();
  }
}

export default async function prepararAmbiente() {
  if (await apiResponde("/health/live"))
    throw new Error(
      `Já existe uma API em ${API} (a de desenvolvimento ou uma API órfã de um e2e interrompido). As jornadas precisam da API isolada (banco NapE2E). ${COMO_LIBERAR_PORTA}`,
    );

  const senhaSql = parseEnv(readFileSync(join(RAIZ, ".env"), "utf8")).NAP_SQL_SENHA;
  if (!senhaSql) throw new Error("Defina NAP_SQL_SENHA no .env da raiz do repositório.");

  // Seed sempre do zero. A senha do sa já está no contêiner: não passa pela linha de comando.
  docker(
    "sqlserver",
    `/opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "IF DB_ID(N'NapE2E') IS NOT NULL BEGIN ALTER DATABASE [NapE2E] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [NapE2E]; END"`,
  );
  // O cache usa chaves por perfil, não por banco: um banco lógico próprio evita ler dados da API de desenvolvimento.
  docker("redis", "redis-cli -n 1 FLUSHDB");

  // Regras do Identity: 10+ caracteres, com dígito e maiúscula.
  const senha = `E2e9${randomBytes(12).toString("hex")}`;
  process.env.E2E_SENHA = senha;

  // Documentos anexados nas jornadas ficam numa pasta temporária da execução, não em backend/arquivos.
  const pastaArquivos = mkdtempSync(join(tmpdir(), "nap-arquivos-e2e-"));

  const log = openSync(LOG_API, "w");
  const api = spawn("dotnet", ["run", "--project", "../backend/src/Nap.Api", "--launch-profile", "Nap.Api", "--", "--migrar", "--semear"], {
    cwd: resolve(import.meta.dirname, ".."),
    stdio: ["ignore", log, log],
    detached: process.platform !== "win32",
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: "Development",
      // Sem nós de MSBuild residentes depois do build do `dotnet run`.
      MSBUILDDISABLENODEREUSE: "1",
      ConnectionStrings__Nap: `Server=127.0.0.1,1433;Database=NapE2E;User Id=sa;Password=${senhaSql};TrustServerCertificate=True`,
      ConnectionStrings__Redis: "127.0.0.1:6379,defaultDatabase=1",
      Seed__SenhaPadrao: senha,
      Jwt__Chave: randomBytes(64).toString("base64"),
      // Em Development a API ainda carrega user-secrets e appsettings.Development.json; variáveis de ambiente
      // vencem ambos. Fixa aqui os demais valores escalares que mudam o comportamento testado.
      Jwt__Emissor: "nap",
      Jwt__Audiencia: "nap",
      // Varredura da outbox a cada 1 s: as notificações chegam a tempo das jornadas.
      Nap__Outbox__IntervaloSegundos: "1",
      Nap__Arquivos__Diretorio: pastaArquivos,
      // As jornadas fazem muitos logins por minuto do mesmo IP.
      Nap__LimiteLogin__Tentativas: "100000",
    },
  });

  // Migração e seed rodam antes de a API escutar: /health/ready respondendo = banco pronto.
  const prazo = Date.now() + PRAZO_API_MS;
  while (!(await apiResponde("/health/ready"))) {
    if (api.exitCode !== null || Date.now() > prazo) {
      encerrar(api);
      closeSync(log);
      rmSync(pastaArquivos, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
      throw new Error(`A API do e2e não ficou pronta (código ${api.exitCode ?? "timeout"}). Veja ${LOG_API}.`);
    }
    await new Promise((r) => setTimeout(r, 1_000));
  }

  return () => {
    encerrar(api);
    closeSync(log);
    rmSync(pastaArquivos, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
  };
}
