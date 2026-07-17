export interface AnuncioBuscaResponse {
  id: string
  marca: string | null
  modelo: string | null
  ano: number | null
  versao: string | null
  preco: number | null
  estado: string | null
  cidade: string | null
  fotos: string[]
  patrocinado: boolean
}

// Story 3.5 — espelha AnuncioDetalheResponse do backend. Diferente de AnuncioBuscaResponse,
// inclui `descricao` (a AC do detalhe exige "todos os campos da Ficha, sem omissão")
export interface AnuncioDetalheResponse {
  id: string
  marca: string | null
  modelo: string | null
  ano: number | null
  versao: string | null
  preco: number | null
  descricao: string | null
  estado: string | null
  cidade: string | null
  fotos: string[]
}

// vazio = Relevância (padrão do backend); demais valores em kebab-case combinam com
// BuscaController.ParaOrdenacao (Story 3.2) — union literal, não string solta, pra pegar
// erros de digitação em tempo de compilação (achado no code review)
export type OrdenacaoBuscaParam = '' | 'preco-asc' | 'preco-desc' | 'ano-asc' | 'ano-desc'

// 7º campo (Versão) além dos 6 do mockup hero-search.html (UX-DR7 mostra só 6 células) —
// achado no code review: a Story/epics.md pedem explicitamente "filtrar por... versão", e o
// PO decidiu priorizar o texto da AC sobre o layout do mockup em vez de deixar Versão sem UI
export interface FiltroBuscaParams {
  marca: string
  modelo: string
  ano: string
  versao: string
  precoMin: string
  precoMax: string
  estado: string
  cidade: string
  ordenarPor: OrdenacaoBuscaParam
  termoLivre: string
}

export const FILTRO_BUSCA_VAZIO: FiltroBuscaParams = {
  marca: '',
  modelo: '',
  ano: '',
  versao: '',
  precoMin: '',
  precoMax: '',
  estado: '',
  cidade: '',
  ordenarPor: '',
  termoLivre: '',
}
