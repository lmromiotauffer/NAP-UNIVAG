import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { loadEnv } from "vite";
import { configDefaults, defineConfig } from "vitest/config";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "VITE_");
  const alvo = env.VITE_API_PROXY || "https://localhost:7180";
  const proxy = { target: alvo, secure: false, changeOrigin: false };
  return {
    plugins: [react(), tailwindcss()],
    server: { proxy: { "/api": proxy, "/health": proxy } },
    test: { environment: "node", globals: true, setupFiles: ["./src/test/setup.ts"], exclude: [...configDefaults.exclude, "e2e/**"] },
  };
});
