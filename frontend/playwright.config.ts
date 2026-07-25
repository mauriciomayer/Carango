import { defineConfig, devices } from '@playwright/test'

// Story E2E (Playwright) — primeira suíte de teste de navegador do projeto. Roda contra os
// servidores de dev já no ar (frontend em BASE_URL, backend por trás do proxy do Vite) — não
// sobe/derruba nada sozinho, mesmo espírito de "ambiente já rodando" usado nos smoke tests
// manuais desta sessão. ignoreHTTPSErrors: true porque o backend usa o certificado de
// desenvolvimento do ASP.NET Core (autoassinado)
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: {
    timeout: 15_000,
  },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: [['html', { open: 'never' }], ['junit', { outputFile: 'e2e-results/junit.xml' }], ['list']],
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:5173',
    ignoreHTTPSErrors: true,
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
