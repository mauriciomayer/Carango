export interface VeiculoReferenciaItem {
  codigo: string
  nome: string
}

interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}

// mesmo padrão de AnuncioApiError (features/anuncios/api.ts) — reaproveitado fora de uma feature
// porque marcas/modelos são dado de referência público, sem posse (Story 2.6)
export class VeiculoReferenciaApiError extends Error {
  readonly problema: ProblemDetails

  constructor(problema: ProblemDetails) {
    super(problema.detail ?? problema.title ?? 'Não foi possível carregar os dados. Tente novamente.')
    this.problema = problema
  }
}

async function tratarResposta<T>(resposta: Response): Promise<T> {
  if (!resposta.ok) {
    const problema = (await resposta.json().catch(() => ({}))) as ProblemDetails
    throw new VeiculoReferenciaApiError(problema)
  }
  return (await resposta.json()) as T
}

// endpoint público (sem [Authorize] no backend) — nenhum token no header, mesmo consumido por
// telas autenticadas (Criar/Editar Anúncio) ou não (Busca, Stories 3.7/3.8)
export async function listarMarcasFipe(): Promise<VeiculoReferenciaItem[]> {
  const resposta = await fetch('/api/veiculos-referencia/marcas')
  return tratarResposta<VeiculoReferenciaItem[]>(resposta)
}

export async function listarModelosFipe(marcaCodigo: string): Promise<VeiculoReferenciaItem[]> {
  const resposta = await fetch(`/api/veiculos-referencia/modelos?marca=${encodeURIComponent(marcaCodigo)}`)
  return tratarResposta<VeiculoReferenciaItem[]>(resposta)
}
