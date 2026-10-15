import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { fileURLToPath } from "node:url";
import { copyFileSync } from "node:fs";
import { resolve } from "node:path";

const output = fileURLToPath(
  new URL("../src/SPWPOS.Api/wwwroot/app/", import.meta.url),
);
let resolvedOutput = output;
export default defineConfig({
  base: "/app/",
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
        copyFileSync(
          `${resolvedOutput}/index.html`,
          `${resolvedOutput}/../index.html`,
        );
      },
    },
  ],
  build: { outDir: output, emptyOutDir: true },
  test: {
    environment: "jsdom",
    setupFiles: ["./tests/setup.js"],
    restoreMocks: true,
  },
});
