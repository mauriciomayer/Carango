import type { Page } from '@playwright/test'

const API_URL = process.env.API_URL ?? 'https://localhost:7090'

export interface ContaDemo {
  email: string
  senha: string
}

// conta criada nesta sessão especificamente pra demonstração/E2E — Lojista com Plano Lojista
// ativo e o catálogo de 18 Anúncios reais já atribuído a ela (ver conversa: "Qual é mesmo a
// conta e senha de teste?"). Fixa de propósito — testes de infraestrutura não geram Vendedor
// novo por execução, reaproveitam a mesma conta estável
export const LOJISTA_DEMO: ContaDemo = {
  email: 'demo.lojista@carango.com.br',
  senha: 'Demo@2026',
}

// autentica via API real (mesmo endpoint POST /api/vendedores/login que o formulário de login
// usa) e injeta o token/tipo direto no localStorage antes da página carregar — evita repetir o
// fluxo de UI de login em todo teste que só precisa de uma sessão já autenticada. Mesmas chaves
// de authStorage.ts (carango.token/carango.tipo) — se essas chaves mudarem, este helper quebra
// junto, o que é o comportamento certo (não duplica a lógica de storage, só as chaves)
export async function autenticarComo(page: Page, conta: ContaDemo): Promise<void> {
  const resposta = await page.request.post(`${API_URL}/api/vendedores/login`, {
    data: { Email: conta.email, Senha: conta.senha },
  })

  if (!resposta.ok()) {
    throw new Error(`Falha ao autenticar como ${conta.email}: HTTP ${resposta.status()}`)
  }

  const corpo = (await resposta.json()) as { token: string; vendedor: { tipo: string } }

  await page.addInitScript(
    ({ token, tipo }) => {
      window.localStorage.setItem('carango.token', token)
      window.localStorage.setItem('carango.tipo', tipo)
    },
    { token: corpo.token, tipo: corpo.vendedor.tipo },
  )
}
