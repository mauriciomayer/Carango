import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
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
    },
  },
})
