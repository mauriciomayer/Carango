import type { OpcaoCombobox } from './ComboboxCascata'

interface DatasetIbge {
  estados: { sigla: string; nome: string }[]
  municipios: Record<string, string[]>
}

// dataset estático embarcado (AD-12) — gerado uma única vez a partir da API pública do IBGE
// (ver frontend/scripts/gerar-dataset-ibge.py), servido como asset de public/ sem passar por
// nenhum backend novo. Cache em módulo — a primeira chamada busca os ~29KB gzip, as seguintes
// reaproveitam a mesma referência em memória. Achado no code review: "dataset estático" não
// significa "sem chamada de rede" — fetch() de um asset do próprio domínio ainda pode falhar
// (conexão ruim, CDN, cache corrompido); ao contrário da versão anterior, uma falha aqui NÃO
// fica cacheada pra sempre — a próxima chamada tenta buscar de novo
let datasetCache: Promise<DatasetIbge> | null = null

function carregarDataset(): Promise<DatasetIbge> {
  datasetCache ??= fetch('/ibge-localidades.json')
    .then((resposta) => {
      if (!resposta.ok) throw new Error(`Falha ao carregar ibge-localidades.json: ${resposta.status}`)
      return resposta.json() as Promise<DatasetIbge>
    })
    .catch((erro: unknown) => {
      datasetCache = null
      throw erro
    })
  return datasetCache
}

export async function listarEstados(): Promise<OpcaoCombobox[]> {
  const dataset = await carregarDataset()
  return dataset.estados.map((estado) => ({ codigo: estado.sigla, nome: estado.nome }))
}

export async function listarMunicipios(siglaEstado: string): Promise<OpcaoCombobox[]> {
  const dataset = await carregarDataset()
  const nomes = dataset.municipios[siglaEstado] ?? []
  // município não tem um "código" de verdade neste dataset reduzido — o próprio nome faz esse
  // papel (único dentro do estado, e é o mesmo valor que acaba persistido em Anuncio.Cidade)
  return nomes.map((nome) => ({ codigo: nome, nome }))
}
