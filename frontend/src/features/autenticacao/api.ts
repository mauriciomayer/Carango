import type { CadastroVendedorRequest, LoginVendedorRequest, LoginVendedorResponse, ProblemDetails, VendedorResponse } from './types'

export class AutenticacaoApiError extends Error {
  readonly problema: ProblemDetails

  constructor(problema: ProblemDetails, mensagemPadrao: string) {
    super(problema.detail ?? problema.title ?? mensagemPadrao)
    this.problema = problema
  }
}

async function lancarErroDaResposta(resposta: Response, mensagemPadrao: string): Promise<never> {
  const problema = (await resposta.json().catch(() => ({}))) as ProblemDetails
  throw new AutenticacaoApiError(problema, mensagemPadrao)
}

export async function cadastrarVendedor(request: CadastroVendedorRequest): Promise<VendedorResponse> {
  const resposta = await fetch('/api/vendedores', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })

  if (!resposta.ok) {
    return lancarErroDaResposta(resposta, 'Não foi possível concluir o cadastro. Tente novamente.')
  }

  return (await resposta.json()) as VendedorResponse
}

export async function autenticarVendedor(request: LoginVendedorRequest): Promise<LoginVendedorResponse> {
  const resposta = await fetch('/api/vendedores/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })

  if (!resposta.ok) {
    return lancarErroDaResposta(resposta, 'Não foi possível entrar. Tente novamente.')
  }

  return (await resposta.json()) as LoginVendedorResponse
}
