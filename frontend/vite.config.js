import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { fileURLToPath } from "node:url";
import { copyFileSync } from "node:fs";

const output = fileURLToPath(
  new URL("../src/SPWPOS.Api/wwwroot/app/", import.meta.url),
);
export default defineConfig({
  base: "/app/",
  plugins: [
    react(),
    tailwindcss(),
    {
      name: "same-origin-entry",
      writeBundle() {
        copyFileSync(`${output}/index.html`, `${output}/../index.html`);
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
