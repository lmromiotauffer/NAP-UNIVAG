import { defineConfig, devices } from "@playwright/test";

// Porta própria: não colide com o `npm run dev` do dia a dia (5173). Padrão 5174; `PORTA_E2E` troca se estiver ocupada.
const PORTA_VITE = Number(process.env.PORTA_E2E ?? 5174);

export default defineConfig({
  testDir: "./e2e",
  // As jornadas gravam no mesmo banco que os perfis leem: execução em série, sem novas tentativas.
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  reporter: [["list"]],
  globalSetup: "./e2e/global-setup.ts",
  use: {
    baseURL: `http://localhost:${PORTA_VITE}`,
    locale: "pt-BR",
    timezoneId: "America/Cuiaba",
    trace: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: `npm run dev -- --port ${PORTA_VITE} --strictPort`,
    url: `http://localhost:${PORTA_VITE}`,
    reuseExistingServer: false,
    env: { VITE_API_PROXY: "https://localhost:7180" },
  },
});
