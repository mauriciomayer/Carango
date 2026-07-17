export type StatusPlanoLojista = 'Ativo' | 'Cancelado'

export interface PlanoLojistaResponse {
  id: string
  status: StatusPlanoLojista
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}
