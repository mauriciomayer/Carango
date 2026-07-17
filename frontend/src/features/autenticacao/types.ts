export type TipoVendedor = 'PessoaFisica' | 'Lojista'

// Story 4.4 — fonte única do rótulo do painel de Anúncios do Vendedor autenticado (usado no
// título da tela, no link do header da Busca e nos links "Voltar aos..." de Criar/Editar Anúncio).
// Achado no code review: cada chamador calculava esse mesmo texto de forma independente
export type RotuloPainel = 'Meus Anúncios' | 'Painel do Lojista'

export function rotuloPainel(tipo: TipoVendedor | null): RotuloPainel {
  return tipo === 'Lojista' ? 'Painel do Lojista' : 'Meus Anúncios'
}

export interface CadastroVendedorRequest {
  email: string
  senha: string
  tipo: TipoVendedor
  telefone?: string
  cnpjRazaoSocial?: string
}

export interface VendedorResponse {
  id: string
  email: string
  tipo: TipoVendedor
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}

export interface LoginVendedorRequest {
  email: string
  senha: string
}

export interface LoginVendedorResponse {
  token: string
  expiraEm: string
  vendedor: VendedorResponse
}
