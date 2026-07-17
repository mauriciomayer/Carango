import type { TipoVendedor } from './types'

// localStorage — não há mecanismo de refresh de JWT decidido pela Arquitetura (Deferred), então um
// armazenamento simples e persistente entre reloads é a escolha pragmática aqui. Sem cookie httpOnly:
// isso exigiria decisões de CSRF/hospedagem ainda em aberto pela Arquitetura (ver 1-3-login-de-vendedor.md).
const CHAVE_TOKEN = 'carango.token'
// Story 4.4 — mesmo padrão da chave do token: persiste entre reloads, chave própria, sem
// mecanismo de expiração dedicado (segue o token)
const CHAVE_TIPO = 'carango.tipo'

export function salvarToken(token: string): void {
  localStorage.setItem(CHAVE_TOKEN, token)
}

export function obterToken(): string | null {
  return localStorage.getItem(CHAVE_TOKEN)
}

export function removerToken(): void {
  localStorage.removeItem(CHAVE_TOKEN)
}

export function salvarTipo(tipo: TipoVendedor): void {
  localStorage.setItem(CHAVE_TIPO, tipo)
}

// achado no code review: sem validar o valor lido, um localStorage corrompido/adulterado (ou uma
// sessão de antes da Story 4.4, sem essa chave ainda) virava um TipoVendedor "de fé" sem checagem —
// mesmo espírito do TryParse já usado no backend (AnunciosController) pra claims do JWT
export function obterTipo(): TipoVendedor | null {
  const valor = localStorage.getItem(CHAVE_TIPO)
  return valor === 'PessoaFisica' || valor === 'Lojista' ? valor : null
}
