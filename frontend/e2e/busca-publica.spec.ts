import { expect, test } from '@playwright/test'

test.describe('Busca pública', () => {
  test('carrega o catálogo com os anúncios existentes', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByText(/veículos? dispon[íi]ve(l|is)/i)).toBeVisible()
    await expect(page.locator('.listing-card').first()).toBeVisible()
  })

  // achado nesta sessão: cache da integração Fipe podia ficar "envenenado" por 24h após uma
  // falha passageira, e (causa raiz de verdade) o host da Fipe tem IPv6 sem rota nesta rede —
  // o HttpClient do .NET tentava IPv6 primeiro e travava até o timeout, sempre. Este teste
  // sobe o navegador de ponta a ponta e falha se qualquer uma das duas regressões voltar.
  // exact: true nos getByLabel dos campos — sem isso, "Marca"/"Modelo"/"Descrição" também
  // combinam por substring com o aria-label do campo de busca livre ("Buscar por marca,
  // modelo, versão ou descrição"), causando strict mode violation (2 elementos)
  test('regressão: selecionar uma Marca carrega o dropdown de Modelo (integração Fipe)', async ({ page }) => {
    await page.goto('/')

    await page.getByLabel('Marca', { exact: true }).click()
    await page.getByRole('option', { name: 'Honda', exact: true }).click()

    await page.getByLabel('Modelo', { exact: true }).click()

    await expect(page.getByText('Não foi possível carregar')).not.toBeVisible()
    // Fipe nunca tem um modelo chamado exatamente "Civic" — sempre vem com a versão/motor
    // (ex. "Civic EXL 2.0 Flex 16V Aut.") — basta confirmar que ALGUM modelo Civic apareceu
    await expect(page.getByRole('option', { name: /^Civic/ }).first()).toBeVisible({ timeout: 10_000 })
  })

  test('filtro de Estado carrega o dropdown de Cidade (dataset IBGE)', async ({ page }) => {
    await page.goto('/')

    await page.getByLabel('Estado', { exact: true }).click()
    await page.getByRole('option', { name: 'São Paulo', exact: true }).click()

    await page.getByLabel('Cidade', { exact: true }).click()
    await expect(page.getByRole('option', { name: 'Campinas', exact: true })).toBeVisible()
  })
})
