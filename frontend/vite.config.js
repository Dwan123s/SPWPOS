import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { fileURLToPath } from "node:url";
import { copyFileSync, cpSync } from "node:fs";
import { resolve } from "node:path";

const output = fileURLToPath(
  new URL("../src/SPWPOS.Api/wwwroot/app/", import.meta.url),
);
const standalone = process.env.PLANORA_STANDALONE === "true";
let resolvedOutput = output;
export default defineConfig({
  base: standalone ? "/" : "/app/",
  plugins: [
    react(),
    tailwindcss(),
    {
      name: "same-origin-entry",
      configResolved(config) {
        // Honor --outDir for isolated verification builds; never copy the
        // preview entry into the running API's wwwroot as a side effect.
        resolvedOutput = resolve(config.root, config.build.outDir);
      },
      writeBundle() {
        if (standalone) {
          const assets = fileURLToPath(new URL("../src/SPWPOS.Api/wwwroot/", import.meta.url));
          cpSync(`${assets}/assets`, `${resolvedOutput}/assets`, { recursive: true });
          cpSync(`${assets}/brand`, `${resolvedOutput}/brand`, { recursive: true });
          return;
        }
        copyFileSync(
          `${resolvedOutput}/index.html`,
          `${resolvedOutput}/../index.html`,
        );
      },
    },
  ],
  build: { outDir: standalone ? "dist" : output, emptyOutDir: true },
  test: {
    environment: "jsdom",
    setupFiles: ["./tests/setup.js"],
    restoreMocks: true,
  },
});
