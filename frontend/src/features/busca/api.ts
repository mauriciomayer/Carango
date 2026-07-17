import type { AnuncioBuscaResponse, AnuncioDetalheResponse, FiltroBuscaParams } from './types'

export class BuscaApiError extends Error {
  constructor() {
    super('Não foi possível carregar os resultados da busca. Tente novamente.')
  }
}

// carrega se a falha foi um 404 (Anúncio removido/inexistente/não-Ativo) — o componente usa
// isso pra decidir entre "Voltar à Busca" (404, recarregar não muda nada) e "Tentar novamente"
// (outra falha, pode ser transitória)
export class AnuncioDetalheApiError extends Error {
  readonly naoEncontrado: boolean

  constructor(naoEncontrado: boolean) {
    super(
      naoEncontrado
        ? 'Este Anúncio não está mais disponível.'
        : 'Não foi possível carregar os dados do Anúncio. Tente novamente.',
    )
    this.naoEncontrado = naoEncontrado
  }
}

// Sem token de autenticação — GET /api/busca é o primeiro endpoint público do projeto,
// nenhum Comprador precisa de conta pra buscar
export async function buscarAnuncios(filtro: FiltroBuscaParams, pagina = 1): Promise<AnuncioBuscaResponse[]> {
  const parametros = new URLSearchParams()
  if (filtro.marca) parametros.set('marca', filtro.marca)
  if (filtro.modelo) parametros.set('modelo', filtro.modelo)
  if (filtro.ano) parametros.set('ano', filtro.ano)
  if (filtro.versao) parametros.set('versao', filtro.versao)
  if (filtro.precoMin) parametros.set('precoMin', filtro.precoMin)
  if (filtro.precoMax) parametros.set('precoMax', filtro.precoMax)
  if (filtro.estado) parametros.set('estado', filtro.estado)
  if (filtro.cidade) parametros.set('cidade', filtro.cidade)
  if (filtro.ordenarPor) parametros.set('ordenarPor', filtro.ordenarPor)
  if (filtro.termoLivre) parametros.set('termo', filtro.termoLivre)
  if (pagina > 1) parametros.set('pagina', String(pagina))

  const query = parametros.toString()
  const resposta = await fetch(`/api/busca${query ? `?${query}` : ''}`)

  if (!resposta.ok) {
    throw new BuscaApiError()
  }

  return (await resposta.json()) as AnuncioBuscaResponse[]
}

// Sem token de autenticação — GET /api/busca/{id} é público, mesmo padrão de buscarAnuncios
export async function obterDetalheAnuncio(id: string): Promise<AnuncioDetalheResponse> {
  const resposta = await fetch(`/api/busca/${id}`)

  if (!resposta.ok) {
    throw new AnuncioDetalheApiError(resposta.status === 404)
  }

  return (await resposta.json()) as AnuncioDetalheResponse
}
