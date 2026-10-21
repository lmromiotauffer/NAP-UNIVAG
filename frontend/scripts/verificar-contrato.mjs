import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import openapiTS, { astToString } from "openapi-typescript";

const esquema = new URL("../../backend/openapi.json", import.meta.url);
const arquivo = new URL("../src/lib/api-schema.d.ts", import.meta.url);

const gerado = astToString(await openapiTS(esquema));
const versionado = readFileSync(fileURLToPath(arquivo), "utf8");

// A CLI antepara um cabeçalho e normaliza quebras de linha; compara ignorando isso.
const normalizar = (t) => t.replace(/\r\n/g, "\n").replace(/^\/\*\*[\s\S]*?\*\/\n+/, "").trim();

if (normalizar(gerado) !== normalizar(versionado)) {
  console.error("Contrato desatualizado: rode npm run api:gerar");
  process.exit(1);
}
console.log("Contrato em dia.");
