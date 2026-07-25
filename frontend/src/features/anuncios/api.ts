import { obterToken } from '../autenticacao/authStorage'
import { tratarSessaoExpirada } from '../../shared/sessaoAutenticada'
import type { AnuncioResponse, ProblemDetails } from './types'

export class AnuncioApiError extends Error {
  readonly problema: ProblemDetails

  constructor(problema: ProblemDetails) {
    // mensagem genérica — esta classe é reaproveitada por criar/editar/pausar/reativar/vender/excluir,
    // então o fallback não pode presumir qual ação estava em andamento quando a resposta não trouxe detail/title
    super(problema.detail ?? problema.title ?? 'Não foi possível concluir a operação. Tente novamente.')
    this.problema = problema
  }
}

// achado em teste manual do usuário: extraído depois de virar a 8ª repetição do mesmo bloco
// if (!resposta.ok) {...} neste arquivo — também é o único lugar que precisa saber de 401
// (sessão expirada, ver sessaoAutenticada.ts), então centralizar aqui evita repetir a checagem
// em cada uma das 8 funções que chamam fetch
async function lancarSeErro(resposta: Response): Promise<void> {
  if (resposta.ok) return
  // achado no code review: page está recarregando (tratarSessaoExpirada dispara o reload) —
  // uma Promise que nunca resolve evita que o chamador chegue a renderizar um erro que vai
  // sumir da tela no instante seguinte de qualquer jeito
  if (tratarSessaoExpirada(resposta)) return new Promise(() => {})
  const problema = (await resposta.json().catch(() => ({}))) as ProblemDetails
  throw new AnuncioApiError(problema)
}

export interface CriarAnuncioParams {
  marca: string
  modelo: string
  ano: string
  versao: string
  preco: string
  descricao: string
  estado: string
  cidade: string
  publicar: boolean
  fotos: File[]
}

export interface EditarAnuncioParams {
  marca: string
  modelo: string
  ano: number | null
  versao: string
  preco: number | null
  descricao: string
  estado: string
  cidade: string
}

export async function listarMeusAnuncios(): Promise<AnuncioResponse[]> {
  const token = obterToken()
  const resposta = await fetch('/api/anuncios', {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })

  await lancarSeErro(resposta)

  return (await resposta.json()) as AnuncioResponse[]
}

export async function obterAnuncio(id: string): Promise<AnuncioResponse> {
  const token = obterToken()
  const resposta = await fetch(`/api/anuncios/${id}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })

  await lancarSeErro(resposta)

  return (await resposta.json()) as AnuncioResponse
}

export async function editarAnuncio(id: string, params: EditarAnuncioParams): Promise<AnuncioResponse> {
  const token = obterToken()
  const resposta = await fetch(`/api/anuncios/${id}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(params),
  })

  await lancarSeErro(resposta)

  return (await resposta.json()) as AnuncioResponse
}

// as 3 transições de status não têm corpo de requisição e são estruturalmente idênticas —
// extraído aqui pra não repetir a mesma chamada fetch 3 vezes
async function postAcaoAnuncio(id: string, acao: string): Promise<AnuncioResponse> {
  const token = obterToken()
  const resposta = await fetch(`/api/anuncios/${id}/${acao}`, {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })

  await lancarSeErro(resposta)

  return (await resposta.json()) as AnuncioResponse
}

export function pausarAnuncio(id: string): Promise<AnuncioResponse> {
  return postAcaoAnuncio(id, 'pausar')
}

export function reativarAnuncio(id: string): Promise<AnuncioResponse> {
  return postAcaoAnuncio(id, 'reativar')
}

export function marcarAnuncioVendido(id: string): Promise<AnuncioResponse> {
  return postAcaoAnuncio(id, 'marcar-vendido')
}

export function destacarAnuncio(id: string): Promise<AnuncioResponse> {
  return postAcaoAnuncio(id, 'destacar')
}

export async function excluirAnuncio(id: string): Promise<void> {
  const token = obterToken()
  const resposta = await fetch(`/api/anuncios/${id}`, {
    method: 'DELETE',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })

  await lancarSeErro(resposta)

  // 204 No Content — sem corpo de resposta pra parsear
}

// Story 2.8 — adiciona fotos a um Anúncio já existente, ação independente de editarAnuncio (que
// usa Content-Type: application/json e não comporta multipart no mesmo corpo)
export async function adicionarFotosAnuncio(id: string, fotos: File[]): Promise<AnuncioResponse> {
  const formData = new FormData()
  for (const foto of fotos) {
    formData.append('Fotos', foto)
  }

  const token = obterToken()
  const resposta = await fetch(`/api/anuncios/${id}/fotos`, {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    body: formData,
  })

  await lancarSeErro(resposta)

  return (await resposta.json()) as AnuncioResponse
}

export async function removerFotoAnuncio(id: string, fotoId: string): Promise<AnuncioResponse> {
  const token = obterToken()
  const resposta = await fetch(`/api/anuncios/${id}/fotos/${fotoId}`, {
    method: 'DELETE',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })

  await lancarSeErro(resposta)

  return (await resposta.json()) as AnuncioResponse
}

export async function criarAnuncio(params: CriarAnuncioParams): Promise<AnuncioResponse> {
  const formData = new FormData()
  formData.append('Marca', params.marca)
  formData.append('Modelo', params.modelo)
  formData.append('Ano', params.ano)
  formData.append('Versao', params.versao)
  formData.append('Preco', params.preco)
  formData.append('Descricao', params.descricao)
  formData.append('Estado', params.estado)
  formData.append('Cidade', params.cidade)
  formData.append('Publicar', String(params.publicar))
  for (const foto of params.fotos) {
    formData.append('Fotos', foto)
  }

  // primeira chamada autenticada do frontend — Content-Type não é setado manualmente:
  // o browser define o boundary do multipart sozinho a partir do FormData
  const token = obterToken()
  const resposta = await fetch('/api/anuncios', {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    body: formData,
  })

  await lancarSeErro(resposta)

  return (await resposta.json()) as AnuncioResponse
}
