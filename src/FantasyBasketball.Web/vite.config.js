import { defineConfig } from 'vite';

export default defineConfig({
  base: '/app/',
  build: { outDir: '../FantasyBasketball.Api/wwwroot/app', emptyOutDir: true },
  server: { proxy: { '/api': { target: process.env.FB_API_URL || 'http://localhost:5000' } } },
});
