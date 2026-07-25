import { obterToken } from '../autenticacao/authStorage'
import { tratarSessaoExpirada } from '../../shared/sessaoAutenticada'
import type { PlanoLojistaResponse, ProblemDetails } from './types'

export class AssinaturaApiError extends Error {
  readonly problema: ProblemDetails

  constructor(problema: ProblemDetails) {
    super(problema.detail ?? problema.title ?? 'Não foi possível concluir a operação. Tente novamente.')
    this.problema = problema
  }
}

// achado em teste manual do usuário: mesma extração de anuncios/api.ts (401 = sessão expirada,
// ver sessaoAutenticada.ts) — não inclui o caso 404, que os dois chamadores tratam de forma
// diferente entre si (obterMeuPlano trata como resultado válido, cancelarPlanoLojista como erro)
async function lancarSeErro(resposta: Response): Promise<void> {
  if (resposta.ok) return
  if (tratarSessaoExpirada(resposta)) return new Promise(() => {})
  const problema = (await resposta.json().catch(() => ({}))) as ProblemDetails
  throw new AssinaturaApiError(problema)
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

  await lancarSeErro(resposta)

  return (await resposta.json()) as PlanoLojistaResponse
}

export async function cancelarPlanoLojista(): Promise<PlanoLojistaResponse> {
  const token = obterToken()
  const resposta = await fetch('/api/planos-lojista/cancelar', {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })

  await lancarSeErro(resposta)

  return (await resposta.json()) as PlanoLojistaResponse
}
