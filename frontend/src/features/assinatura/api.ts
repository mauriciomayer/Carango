import { obterToken } from '../autenticacao/authStorage'
import type { PlanoLojistaResponse, ProblemDetails } from './types'

export class AssinaturaApiError extends Error {
  readonly problema: ProblemDetails

  constructor(problema: ProblemDetails) {
    super(problema.detail ?? problema.title ?? 'Não foi possível concluir a operação. Tente novamente.')
    this.problema = problema
  }
}

// null é um resultado válido aqui (Vendedor nunca assinou um Plano Lojista), não um erro — mesmo
// espírito do backend (GerenciarPlanoLojistaService.ObterAsync retorna null, o Controller nunca
// usa Problem() pro 404 deste endpoint especificamente)
export async function obterMeuPlano(): Promise<PlanoLojistaResponse | null> {
  const token = obterToken()
  const resposta = await fetch('/api/planos-lojista/meu', {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })

  if (resposta.status === 404) return null

  if (!resposta.ok) {
    const problema = (await resposta.json().catch(() => ({}))) as ProblemDetails
    throw new AssinaturaApiError(problema)
  }

  return (await resposta.json()) as PlanoLojistaResponse
}

export async function cancelarPlanoLojista(): Promise<PlanoLojistaResponse> {
  const token = obterToken()
  const resposta = await fetch('/api/planos-lojista/cancelar', {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })

  if (!resposta.ok) {
    const problema = (await resposta.json().catch(() => ({}))) as ProblemDetails
    throw new AssinaturaApiError(problema)
  }

  return (await resposta.json()) as PlanoLojistaResponse
}
