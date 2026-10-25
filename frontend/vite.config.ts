import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// В dev-режиме /api проксируется на локальный бэкенд (dotnet run, порт 5080).
// В проде /api отдаёт nginx (см. deploy/nginx.conf).
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
  server: {
    port: 5173,
    proxy: {
      "/api": { target: "http://localhost:5080", changeOrigin: true, timeout: 180000 },
    },
  },
});
