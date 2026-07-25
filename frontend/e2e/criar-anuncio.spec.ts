import { expect, test } from '@playwright/test'
import { autenticarComo, LOJISTA_DEMO } from './support/helpers/auth'

async function selecionarToyotaCorolla(page: import('@playwright/test').Page) {
  await page.getByLabel('Marca', { exact: true }).click()
  await page.getByRole('option', { name: 'Toyota', exact: true }).click()

  await page.getByLabel('Modelo', { exact: true }).click()
  // Fipe nunca tem um modelo chamado exatamente "Corolla" — sempre vem com a versão/motor
  // (ex. "Corolla XEi 1.8/1.8 Flex 16V Aut."). Filtra pelo campo de busca do próprio combobox
  // e escolhe a primeira opção da lista filtrada, já que a versão exata não importa pro teste
  await page.getByRole('combobox', { name: 'Modelo' }).fill('Corolla')
  await page.getByRole('option', { name: /^Corolla/ }).first().click()
}

test.describe('Criar Anúncio — autenticado como Lojista', () => {
  test.beforeEach(async ({ page }) => {
    await autenticarComo(page, LOJISTA_DEMO)
    await page.goto('/')
    await page.getByRole('button', { name: 'Painel do Lojista' }).click()
    await page.getByRole('button', { name: 'Criar novo Anúncio' }).click()
  })

  // achado nesta sessão: Anuncio.ValidarCamposObrigatorios só checava se Ano estava presente,
  // nunca validava faixa — um Ano tipo 1800 passava por Publicar() sem erro nenhum e ficava
  // persistido de verdade. Este teste sobe o formulário real, publica com Ano fora da faixa e
  // confirma que o servidor rejeita — client-side não bloqueia mais (a garantia é o Domain)
  test('regressão: Ano fora da faixa plausível é rejeitado pelo servidor', async ({ page }) => {
    await selecionarToyotaCorolla(page)

    await page.getByLabel('Ano', { exact: true }).fill('1800')
    await page.getByLabel('Versão', { exact: true }).fill('XEI')
    await page.getByLabel('Preço', { exact: true }).fill('90000')
    await page.getByLabel('Descrição', { exact: true }).fill('Anúncio de teste E2E — Ano inválido, não deve publicar.')

    await page.getByLabel('Estado', { exact: true }).click()
    await page.getByRole('option', { name: 'São Paulo', exact: true }).click()
    await page.getByLabel('Cidade', { exact: true }).click()
    await page.getByRole('option', { name: 'Campinas', exact: true }).click()

    await page.getByRole('button', { name: 'Publicar' }).click()

    await expect(page.getByRole('alert')).toContainText(/ano/i)
    await expect(page.getByText('Anúncio publicado')).not.toBeVisible()
  })

  // achado nesta sessão: Descrição era <input type="text"> de uma linha só. Este teste publica
  // com uma descrição de múltiplas linhas e reabre em edição pra confirmar que nada foi perdido
  test('cria um Anúncio com descrição multi-linha preservada de ponta a ponta', async ({ page }) => {
    await selecionarToyotaCorolla(page)

    const descricaoMultilinha = [
      'Primeira linha da descrição — teste E2E.',
      'Segunda linha, outro detalhe do veículo.',
      'Terceira linha, informação final.',
    ].join('\n')

    await page.getByLabel('Ano', { exact: true }).fill('2022')
    await page.getByLabel('Versão', { exact: true }).fill('XEI')
    await page.getByLabel('Preço', { exact: true }).fill('120000')
    await page.getByLabel('Descrição', { exact: true }).fill(descricaoMultilinha)

    await page.getByLabel('Estado', { exact: true }).click()
    await page.getByRole('option', { name: 'São Paulo', exact: true }).click()
    await page.getByLabel('Cidade', { exact: true }).click()
    await page.getByRole('option', { name: 'Campinas', exact: true }).click()

    await page.getByRole('button', { name: 'Publicar' }).click()

    await expect(page.getByText('Anúncio publicado')).toBeVisible({ timeout: 10_000 })

    await page.getByRole('button', { name: 'Editar este Anúncio' }).click()
    await expect(page.getByLabel('Descrição', { exact: true })).toHaveValue(descricaoMultilinha)
  })
})
