// Extraído no code review da Story 3.5 — 3ª ocorrência de metaDoAnuncio/FORMATADOR_PRECO
// (MeusAnunciosPanel.tsx, BuscaPage.tsx, AnuncioDetalhePage.tsx), mesma disciplina "espera a
// 3ª ocorrência pra extrair" já usada nesta épica. Aceita qualquer formato de Anúncio que tenha
// esses 4 campos (AnuncioResponse/AnuncioBuscaResponse/AnuncioDetalheResponse), via tipagem
// estrutural — não precisa de um tipo compartilhado entre os três contratos de API
export const FORMATADOR_PRECO = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

interface AnuncioComLocalizacao {
  ano: number | null
  versao: string | null
  cidade: string | null
  estado: string | null
}

export function metaDoAnuncio(anuncio: AnuncioComLocalizacao): string {
  const localizacao = [anuncio.cidade, anuncio.estado].filter(Boolean).join('/')
  return [anuncio.ano?.toString(), anuncio.versao, localizacao || null].filter(Boolean).join(' · ')
}
