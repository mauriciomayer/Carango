export type StatusAnuncio = 'Rascunho' | 'Ativo' | 'Pausado' | 'Vendido'

// Story 2.8 — Fotos passou de string[] (só a Url) pra FotoResponse[] (Id + Url), porque editar um
// Anúncio existente precisa do Id de cada foto pra saber qual remover
export interface FotoResponse {
  id: string
  url: string
}

export interface AnuncioResponse {
  id: string
  vendedorId: string
  marca: string | null
  modelo: string | null
  ano: number | null
  versao: string | null
  preco: number | null
  descricao: string | null
  estado: string | null
  cidade: string | null
  status: StatusAnuncio
  fotos: FotoResponse[]
  patrocinado: boolean
  visualizacoes: number
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}
