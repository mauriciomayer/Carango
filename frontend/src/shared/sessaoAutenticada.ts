import { removerToken } from '../features/autenticacao/authStorage'

// achado em teste manual do usuário: sessão expirada (JWT com 60min de validade, Jwt:DuracaoMinutos
// no backend) sempre mostrava o mesmo erro genérico de "Não foi possível concluir a operação",
// preso num loop de "Tentar novamente" que nunca ia funcionar — o token continuava expirado. E não
// existia nenhum jeito de deslogar pela UI: removerToken() (authStorage.ts) nunca era chamada em
// lugar nenhum do app. Compartilhado entre anuncios/api.ts e assinatura/api.ts, os 2 únicos lugares
// com chamada autenticada — 401 limpa o token e recarrega a página, que remonta App.tsx com
// autenticado=false (obterToken() volta null) — cai na Busca pública, "Entrar" abre um login novo
export function tratarSessaoExpirada(resposta: Response): boolean {
  if (resposta.status !== 401) return false
  removerToken()
  window.location.reload()
  return true
}
