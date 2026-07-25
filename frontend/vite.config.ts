import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Story 5.1 — 'vitest/config' reexporta o defineConfig do Vite já com o campo `test` tipado,
  // reaproveitando este mesmo arquivo em vez de duplicar plugins/proxy num vitest.config.ts à parte
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    // e2e/ tem specs do Playwright (test.describe própria, incompatível com a do Vitest) —
    // sem excluir, o glob padrão do Vitest (**/*.spec.*) tenta rodá-los e quebra a suíte inteira
    exclude: ['**/node_modules/**', '**/e2e/**'],
  },
  server: {
    proxy: {
      // porta HTTPS da Api em backend/Api/Properties/launchSettings.json (perfil "https")
      // secure: false — certificado de desenvolvimento do ASP.NET Core não é necessariamente confiável no ambiente
      '/api': {
        target: 'https://localhost:7090',
        changeOrigin: true,
        secure: false,
      },
      // fotos de Anúncio servidas via UseStaticFiles() (LocalDiskMediaStorage, Story 2.1) — primeira
      // vez que o frontend renderiza uma <img> de verdade (Story 2.5); sem isso, /uploads/... daria
      // 404 contra o servidor de dev do Vite (porta diferente do backend)
      '/uploads': {
        target: 'https://localhost:7090',
        changeOrigin: true,
        secure: false,
      },
      // catálogo de fotos reais de demonstração (backend/Api/wwwroot/catalogo/, versionado no Git —
      // diferente de /uploads, que é conteúdo enviado em runtime e fica fora do controle de versão).
      // Servido pelo mesmo UseStaticFiles() do backend, só precisa do mesmo proxy de dev
      '/catalogo': {
        target: 'https://localhost:7090',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
