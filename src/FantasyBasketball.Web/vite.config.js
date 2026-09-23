import { defineConfig } from 'vite';

export default defineConfig({
  base: '/app/',
  build: { outDir: '../FantasyBasketball.Api/wwwroot/app', emptyOutDir: true },
  server: { proxy: Object.fromEntries(['/api', '/img', '/fonts'].map(path => [path, { target: process.env.FB_API_URL || 'http://localhost:5000' }])) },
});
