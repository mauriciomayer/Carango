import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import '../autenticacao/AuthForm.css'
import '../anuncios/MeusAnunciosPanel.css'
import './BuscaPage.css'
import type { RotuloPainel } from '../autenticacao/types'
import { BuscaApiError, buscarAnuncios } from './api'
import { FILTRO_BUSCA_VAZIO } from './types'
import type { AnuncioBuscaResponse, FiltroBuscaParams, OrdenacaoBuscaParam } from './types'
import { FORMATADOR_PRECO, metaDoAnuncio } from '../../shared/anuncioFormatacao'
import { ComboboxCascata, type OpcaoCombobox } from '../../shared/ComboboxCascata'
import { VeiculoReferenciaApiError, listarMarcasFipe, listarModelosFipe } from '../../shared/veiculoReferenciaApi'
import { listarEstados, listarMunicipios } from '../../shared/ibgeLocalidades'

// precisa bater com AnuncioRepository.TamanhoPagina (backend) — usada só pra inferir se a
// última página buscada veio "cheia" (pode haver mais) ou "incompleta" (fim de lista, Story 3.4)
const TAMANHO_PAGINA = 20

// opções sintéticas de "limpar campo" pros ComboboxCascata de Marca/Modelo (Story 3.7) e
// Estado/Cidade (Story 3.8) — achado no code review: valores constantes que nunca dependem de
// props/state, sem motivo pra recriar o objeto a cada render
const OPCAO_TODAS_MARCAS: OpcaoCombobox = { codigo: '', nome: 'Todas as marcas' }
const OPCAO_TODOS_MODELOS: OpcaoCombobox = { codigo: '', nome: 'Todos os modelos' }
const OPCAO_TODOS_ESTADOS: OpcaoCombobox = { codigo: '', nome: 'Todos os estados' }
const OPCAO_TODAS_CIDADES: OpcaoCombobox = { codigo: '', nome: 'Todas as cidades' }

interface BuscaPageProps {
  autenticado: boolean
  // Story 4.4 — rótulo do link do header muda pra "Painel do Lojista" quando o Vendedor
  // autenticado é Lojista, pra bater com o título da tela pra onde o link leva. Já resolvido
  // pelo App.tsx (rotuloPainel), não repetido aqui — achado no code review, cada lugar calculava
  // o mesmo ternário de forma independente
  tituloPainel: RotuloPainel
  onEntrar: () => void
  onMeusAnuncios: () => void
  onSelecionar: (id: string) => void
}

// Sem fallback de "Rascunho sem Ficha": um Anúncio só chega aqui com Status = Ativo, e
// Publicar()/Reativar() garantem Marca/Modelo preenchidos pra qualquer Anúncio Ativo
// (Domain/Anuncio.cs) — por isso não reaproveita a versão de MeusAnunciosPanel.tsx
function tituloDoAnuncio(anuncio: AnuncioBuscaResponse): string {
  return [anuncio.marca, anuncio.modelo].filter(Boolean).join(' ')
}

function ListingCardBusca({ anuncio, onSelecionar }: { anuncio: AnuncioBuscaResponse; onSelecionar: (id: string) => void }) {
  const foto = anuncio.fotos[0]
  const meta = metaDoAnuncio(anuncio)
  // listing-card-sponsored (Story 4.1, UX-DR5) — fundo e canto do corpo, não só o badge;
  // as duas mudanças sempre andam juntas, nunca uma sem a outra (mesma regra do DESIGN.md)
  const classeCard = anuncio.patrocinado ? 'listing-card listing-card--sponsored' : 'listing-card'

  // onClick ativado na Story 3.5 — até então o card não navegava pra lugar nenhum
  // (Detalhe do Anúncio ainda não existia). Estrutura de div externa + button interno
  // (`__area-clicavel`) espelha MeusAnunciosPanel.tsx (Story 4.1) — mesmo sem área de ações
  // aqui, reaproveita o mesmo CSS compartilhado em vez de duplicar o layout do card
  return (
    <div className={classeCard}>
      <button type="button" className="listing-card__area-clicavel" onClick={() => onSelecionar(anuncio.id)}>
        <div className="listing-card__foto-wrapper">
          {foto ? (
            <img className="listing-card__foto" src={foto} alt="" />
          ) : (
            <div className="listing-card__foto" aria-hidden="true" />
          )}
          {/* sponsored-badge (UX-DR6) — canto superior esquerdo da foto, nunca sobre o preço */}
          {anuncio.patrocinado && <span className="sponsored-badge">Anúncio Patrocinado</span>}
        </div>
        <div className="listing-card__corpo">
          <p className="listing-card__titulo">{tituloDoAnuncio(anuncio)}</p>
          {anuncio.preco != null && (
            <p className="listing-card__preco">
              <strong>{FORMATADOR_PRECO.format(anuncio.preco)}</strong>
            </p>
          )}
          {meta && <p className="listing-card__meta">{meta}</p>}
        </div>
      </button>
    </div>
  )
}

export function BuscaPage({ autenticado, tituloPainel, onEntrar, onMeusAnuncios, onSelecionar }: BuscaPageProps) {
  const [filtros, setFiltros] = useState<FiltroBuscaParams>(FILTRO_BUSCA_VAZIO)
  const [filtroAplicado, setFiltroAplicado] = useState<FiltroBuscaParams>(FILTRO_BUSCA_VAZIO)
  const [resultados, setResultados] = useState<AnuncioBuscaResponse[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [tentativa, setTentativa] = useState(0)

  // Estado de paginação (Story 3.4) — `paginaCheia` não é derivado de `resultados.length`
  // sozinho: um total exatamente múltiplo de TAMANHO_PAGINA (ex. 20, 40) ficaria ambíguo entre
  // "acabou aqui" e "pode ter mais" só olhando o tamanho acumulado. Reflete se a ÚLTIMA página
  // buscada individualmente veio com TAMANHO_PAGINA itens (pode haver mais) ou menos (fim de lista)
  const [pagina, setPagina] = useState(1)
  const [paginaCheia, setPaginaCheia] = useState(false)
  const [carregandoMais, setCarregandoMais] = useState(false)
  const [erroCarregarMais, setErroCarregarMais] = useState<string | null>(null)

  // Achado no code review: sem isso, um "Carregar mais" clicado pouco antes de uma nova busca
  // ser disparada (nova submissão de filtro, troca de ordenação) podia resolver DEPOIS que
  // `resultados` já tinha sido substituído pela nova busca, e concatenar itens da busca ANTIGA
  // por cima dos resultados da busca NOVA — lista com critérios misturados, sem nenhum erro
  // visível. `geracaoBusca` incrementa a cada nova busca; aoCarregarMais descarta seu resultado
  // se a geração já tiver mudado quando a resposta chegar
  const geracaoBusca = useRef(0)

  // Story 3.7 — Marca/Modelo via Fipe, reaproveitando 100% o proxy/cache da Story 2.6. Erro/retry
  // completo desde o início (diferente da Story 2.7/IBGE, que precisou de patch no code review —
  // aqui a chamada é uma integração externa de verdade, AD-12, mesmo tratamento de Criar/Editar Anúncio)
  const [marcas, setMarcas] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroMarcas, setErroMarcas] = useState<string | null>(null)
  const [tentativaMarcas, setTentativaMarcas] = useState(0)
  const [marcaCodigo, setMarcaCodigo] = useState<string | null>(null)
  const [modelos, setModelos] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroModelos, setErroModelos] = useState<string | null>(null)
  const [tentativaModelos, setTentativaModelos] = useState(0)

  useEffect(() => {
    let cancelado = false
    setErroMarcas(null)
    setMarcas(undefined)
    listarMarcasFipe()
      .then((lista) => {
        if (!cancelado) setMarcas(lista)
      })
      .catch((erro: unknown) => {
        if (cancelado) return
        setMarcas([])
        setErroMarcas(erro instanceof VeiculoReferenciaApiError ? erro.message : 'Não foi possível carregar as marcas. Tente novamente.')
      })
    return () => {
      cancelado = true
    }
  }, [tentativaMarcas])

  useEffect(() => {
    if (!marcaCodigo) return
    let cancelado = false
    setErroModelos(null)
    setModelos(undefined)
    listarModelosFipe(marcaCodigo)
      .then((lista) => {
        if (!cancelado) setModelos(lista)
      })
      .catch((erro: unknown) => {
        if (cancelado) return
        setModelos([])
        setErroModelos(erro instanceof VeiculoReferenciaApiError ? erro.message : 'Não foi possível carregar os modelos. Tente novamente.')
      })
    return () => {
      cancelado = true
    }
  }, [marcaCodigo, tentativaModelos])

  // achado no code review: "codigo" vazio é a opção sintética "Todas as marcas"/"Todos os
  // modelos" (prependada na renderização, ver abaixo) — sem os <input type="text"> antigos,
  // não existia mais nenhum jeito de voltar um campo pra "qualquer um" sem clicar "Limpar
  // filtros" (que zera os outros 8 campos junto). Mais descobrível que o placeholder antigo:
  // uma opção clicável e explícita na própria lista, não só um texto passivo
  function aoSelecionarMarca(opcao: OpcaoCombobox) {
    if (!opcao.codigo) {
      setFiltros((atual) => ({ ...atual, marca: '', modelo: '' }))
      setMarcaCodigo(null)
      setModelos(undefined)
      setErroModelos(null)
      return
    }
    setFiltros((atual) => ({ ...atual, marca: opcao.nome, modelo: '' }))
    // achado no code review: sem isso, a lista de modelos da Marca anterior ficava em memória
    // por um render até o useEffect reagir à mudança de marcaCodigo — mesma limpeza síncrona
    // que aoLimparFiltros já fazia
    setModelos(undefined)
    setErroModelos(null)
    setMarcaCodigo(opcao.codigo)
  }

  function aoSelecionarModelo(opcao: OpcaoCombobox) {
    setFiltros((atual) => ({ ...atual, modelo: opcao.codigo ? opcao.nome : '' }))
  }

  const marcasComOpcaoTodas = marcas ? [OPCAO_TODAS_MARCAS, ...marcas] : marcas
  const modelosComOpcaoTodos = modelos ? [OPCAO_TODOS_MODELOS, ...modelos] : modelos

  // Story 3.8 — Estado/Cidade via dataset estático do IBGE, reaproveitando 100% a Story 2.7.
  // Erro/retry e opções "Todos os estados"/"Todas as cidades" desde o início — lições das
  // Stories 2.7/3.7 (ver Contexto da story), não como patch depois.
  // Achado no code review: sem estado local separado pra sigla — diferente de Marca/Modelo
  // (onde marcaCodigo, um id numérico da Fipe, é genuinamente diferente de filtros.marca, o
  // nome de exibição), aqui filtros.estado JÁ É a sigla; um "estadoSigla" à parte só duplicava
  // o mesmo valor com uma representação de "vazio" diferente (string vazia vs null)
  const [estados, setEstados] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroEstados, setErroEstados] = useState<string | null>(null)
  const [tentativaEstados, setTentativaEstados] = useState(0)
  const [municipios, setMunicipios] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroMunicipios, setErroMunicipios] = useState<string | null>(null)
  const [tentativaMunicipios, setTentativaMunicipios] = useState(0)

  useEffect(() => {
    let cancelado = false
    setErroEstados(null)
    setEstados(undefined)
    listarEstados()
      .then((lista) => {
        if (!cancelado) setEstados(lista)
      })
      .catch(() => {
        if (cancelado) return
        setEstados([])
        setErroEstados('Não foi possível carregar os estados. Tente novamente.')
      })
    return () => {
      cancelado = true
    }
  }, [tentativaEstados])

  useEffect(() => {
    if (!filtros.estado) return
    let cancelado = false
    setErroMunicipios(null)
    setMunicipios(undefined)
    listarMunicipios(filtros.estado)
      .then((lista) => {
        if (!cancelado) setMunicipios(lista)
      })
      .catch(() => {
        if (cancelado) return
        setMunicipios([])
        setErroMunicipios('Não foi possível carregar as cidades. Tente novamente.')
      })
    return () => {
      cancelado = true
    }
  }, [filtros.estado, tentativaMunicipios])

  function aoSelecionarEstado(opcao: OpcaoCombobox) {
    // achado no code review (2 agentes): sem este guard, reselecionar o mesmo Estado já
    // selecionado fazia setMunicipios(undefined) rodar mas o useEffect acima nunca reexecutava
    // (a dependência filtros.estado não muda de valor, bail-out do React) — Cidade ficava
    // travado em "Carregando…" pra sempre. Cobre também o caso "Todos os estados" (codigo ''
    // reselecionado sobre um filtro já vazio) com o mesmo guard, de graça
    if (opcao.codigo === filtros.estado) return
    setFiltros((atual) => ({ ...atual, estado: opcao.codigo, cidade: '' }))
    setMunicipios(undefined)
    setErroMunicipios(null)
  }

  function aoSelecionarCidade(opcao: OpcaoCombobox) {
    setFiltros((atual) => ({ ...atual, cidade: opcao.codigo ? opcao.nome : '' }))
  }

  const estadosComOpcaoTodos = estados ? [OPCAO_TODOS_ESTADOS, ...estados] : estados
  const municipiosComOpcaoTodas = municipios ? [OPCAO_TODAS_CIDADES, ...municipios] : municipios

  // Achado no code review: zerar `resultados` a cada busca fazia a seção inteira (incluindo o
  // <select> de ordenação) sumir e reaparecer a cada troca de ordenação, tirando o foco do
  // controle a cada reordenação — contradizia a própria intenção de "efeito imediato". Agora
  // `resultados` só é `null` na primeiríssima carga (nunca buscou nada ainda); reordenações e
  // reaplicações de filtro mantêm a lista anterior visível até a nova chegar
  useEffect(() => {
    let cancelado = false
    geracaoBusca.current += 1
    setErro(null)
    setErroCarregarMais(null)
    setPagina(1)
    // reseta o estado de "carregar mais" da busca anterior — impede que uma resposta tardia
    // de um "Carregar mais" descartado (guard de geração acima) deixasse `carregandoMais`
    // travado em true pra sempre, já que aquele finally também respeita a geração
    setCarregandoMais(false)
    setPaginaCheia(false)

    async function carregar() {
      try {
        const lista = await buscarAnuncios(filtroAplicado, 1)
        if (!cancelado) {
          setResultados(lista)
          setPaginaCheia(lista.length === TAMANHO_PAGINA)
        }
      } catch (erroCapturado) {
        if (cancelado) return
        const mensagem = erroCapturado instanceof BuscaApiError ? erroCapturado.message : 'Não foi possível carregar os resultados da busca. Tente novamente.'
        setErro(mensagem)
      }
    }

    void carregar()
    return () => {
      cancelado = true
    }
  }, [filtroAplicado, tentativa])

  // Carregamento incremental (AC #1) — busca só a próxima página e concatena ao que já existe,
  // sem tocar em `resultados` em caso de falha (AC #3: não perder o que já foi carregado)
  async function aoCarregarMais() {
    if (carregandoMais) return // guarda contra duplo clique antes do "disabled" re-renderizar
    setCarregandoMais(true)
    setErroCarregarMais(null)
    const proximaPagina = pagina + 1
    const geracaoNoMomentoDoClique = geracaoBusca.current

    try {
      const lista = await buscarAnuncios(filtroAplicado, proximaPagina)
      // se uma nova busca começou enquanto esta requisição estava em andamento, descarta o
      // resultado — ele pertence aos filtros/ordenação antigos, não aos atuais (achado no code review)
      if (geracaoBusca.current !== geracaoNoMomentoDoClique) return
      setResultados((atual) => [...(atual ?? []), ...lista])
      setPagina(proximaPagina)
      setPaginaCheia(lista.length === TAMANHO_PAGINA)
    } catch {
      if (geracaoBusca.current !== geracaoNoMomentoDoClique) return
      // sempre a mensagem de "carregar mais" aqui, independente do tipo de erro — achado no code
      // review: BuscaApiError já usa a mesma mensagem genérica do carregamento principal, então
      // checar `instanceof BuscaApiError` nunca escolhia a mensagem específica de carregar mais
      setErroCarregarMais('Não foi possível carregar mais resultados. Tente novamente.')
    } finally {
      if (geracaoBusca.current === geracaoNoMomentoDoClique) setCarregandoMais(false)
    }
  }

  function aoSubmeter(evento: FormEvent) {
    evento.preventDefault()
    setFiltroAplicado(filtros)
  }

  function aoLimparFiltros() {
    setFiltros(FILTRO_BUSCA_VAZIO)
    setFiltroAplicado(FILTRO_BUSCA_VAZIO)
    // achado no code review da Story 2.7 (aplicado aqui desde o início): sem isso, o filtro
    // Modelo ficaria com a lista de modelos "presa" de uma Marca que "Limpar filtros" já
    // devia ter esquecido
    setMarcaCodigo(null)
    setModelos(undefined)
    // achado no code review: faltava resetar também o erro — inofensivo hoje só porque o campo
    // fica desabilitado junto, não uma limpeza de verdade
    setErroModelos(null)
    setMunicipios(undefined)
    setErroMunicipios(null)
    // achado no code review: sem isso, uma falha anterior ao carregar estados deixava o
    // combobox Estado quebrado mesmo depois de "Limpar filtros" — só o "Tentar novamente"
    // interno dele resolvia. Agora "Limpar filtros" também força uma nova tentativa
    setTentativaEstados((n) => n + 1)
  }

  function aoAlterarCampo(campo: keyof FiltroBuscaParams, valor: string) {
    setFiltros((atual) => ({ ...atual, [campo]: valor }))
  }

  // Diferente do grid de filtros de campo (só aplica ao clicar em "Buscar"), a ordenação
  // dispara a busca imediatamente — reflete a expectativa comum de "ordenar" em marketplaces
  // e a própria AC #1 da Story 3.2 ("escolho ordenar... então são reordenados")
  function aoAlterarOrdenacao(valor: OrdenacaoBuscaParam) {
    setFiltros((atual) => ({ ...atual, ordenarPor: valor }))
    setFiltroAplicado((atual) => ({ ...atual, ordenarPor: valor }))
  }

  return (
    <div className="busca-page">
      <div className="busca-hero">
        <div className="busca-hero__brandrow">
          <div className="busca-hero__brand">
            Car<span>ango</span>
          </div>
          {autenticado ? (
            <button type="button" className="busca-hero__link" onClick={onMeusAnuncios}>
              {tituloPainel}
            </button>
          ) : (
            <button type="button" className="busca-hero__link" onClick={onEntrar}>
              Entrar
            </button>
          )}
        </div>
        <h1 className="busca-hero__titulo">
          Encontre seu <strong>primeiro carro</strong> com curadoria de verdade.
        </h1>
        <p className="busca-hero__subtitulo">Anúncios verificados, preço justo, sem letra miúda.</p>

        <form className="search-filters" onSubmit={aoSubmeter}>
          {/* Ativado na Story 3.3 — continua dentro do mesmo <form> do grid de filtros
              estruturados, então "Buscar" já inclui o termo livre sem lógica de submit nova */}
          <div className="search-filters__livre">
            <input
              type="text"
              value={filtros.termoLivre}
              onChange={(e) => aoAlterarCampo('termoLivre', e.target.value)}
              placeholder="Buscar por marca, modelo, versão ou descrição"
              aria-label="Buscar por marca, modelo, versão ou descrição"
            />
          </div>

          <div className="search-filters__grid">
            <ComboboxCascata
              label="Marca"
              valor={filtros.marca}
              opcoes={marcasComOpcaoTodas}
              erro={erroMarcas ?? undefined}
              onTentarNovamente={() => setTentativaMarcas((n) => n + 1)}
              onSelecionar={aoSelecionarMarca}
              mensagemSemResultado="Nenhuma marca encontrada."
            />
            <ComboboxCascata
              label="Modelo"
              valor={filtros.modelo}
              opcoes={modelosComOpcaoTodos}
              erro={erroModelos ?? undefined}
              onTentarNovamente={() => setTentativaModelos((n) => n + 1)}
              onSelecionar={aoSelecionarModelo}
              desabilitado={!marcaCodigo}
              placeholderDesabilitado="Escolha a marca primeiro"
              mensagemSemResultado="Nenhum modelo encontrado."
            />
            <label className="search-filters__item">
              <span className="search-filters__rotulo">Ano</span>
              <input
                type="number"
                value={filtros.ano}
                onChange={(e) => aoAlterarCampo('ano', e.target.value)}
                placeholder="Todos"
              />
            </label>
            <label className="search-filters__item">
              <span className="search-filters__rotulo">Versão</span>
              <input
                type="text"
                value={filtros.versao}
                onChange={(e) => aoAlterarCampo('versao', e.target.value)}
                placeholder="Todas"
              />
            </label>
            <div className="search-filters__item search-filters__item--preco">
              <span className="search-filters__rotulo">Preço</span>
              <div className="search-filters__faixa">
                <input
                  type="number"
                  value={filtros.precoMin}
                  onChange={(e) => aoAlterarCampo('precoMin', e.target.value)}
                  placeholder="De"
                  aria-label="Preço mínimo"
                />
                <input
                  type="number"
                  value={filtros.precoMax}
                  onChange={(e) => aoAlterarCampo('precoMax', e.target.value)}
                  placeholder="Até"
                  aria-label="Preço máximo"
                />
              </div>
            </div>
            <ComboboxCascata
              label="Estado"
              valor={filtros.estado}
              opcoes={estadosComOpcaoTodos}
              erro={erroEstados ?? undefined}
              onTentarNovamente={() => setTentativaEstados((n) => n + 1)}
              onSelecionar={aoSelecionarEstado}
              mensagemSemResultado="Nenhum estado encontrado."
            />
            <ComboboxCascata
              label="Cidade"
              valor={filtros.cidade}
              opcoes={municipiosComOpcaoTodas}
              erro={erroMunicipios ?? undefined}
              onTentarNovamente={() => setTentativaMunicipios((n) => n + 1)}
              onSelecionar={aoSelecionarCidade}
              desabilitado={!filtros.estado}
              placeholderDesabilitado="Escolha o estado primeiro"
              mensagemSemResultado="Nenhuma cidade encontrada."
            />
          </div>

          {/* achado do usuário em teste manual: "Limpar filtros" só existia no empty-state (sem
              resultado nenhum) — agora fica sempre visível ao lado de "Buscar", reaproveitando
              aoLimparFiltros já existente. Empilhado full-width no mobile (mesmo visual de
              "Buscar" hoje); lado a lado, menores, alinhados à direita a partir do breakpoint
              web (768px, mesmo já usado no resto deste formulário) */}
          <div className="search-filters__acoes">
            <button type="button" className="search-filters__botao search-filters__botao--secundario" onClick={aoLimparFiltros}>
              Limpar filtros
            </button>
            <button type="submit" className="search-filters__botao">
              Buscar
            </button>
          </div>
        </form>
      </div>

      {erro ? (
        <div className="busca-page__estado">
          <p className="busca-page__erro" role="alert">
            {erro}
          </p>
          <button type="button" className="auth-form__alternar" onClick={() => setTentativa((n) => n + 1)}>
            Tentar novamente
          </button>
        </div>
      ) : resultados === null ? (
        <div className="busca-page__estado" role="status">
          <p className="auth-form__corpo">Carregando…</p>
        </div>
      ) : resultados.length === 0 ? (
        <div className="empty-state">
          <hr className="empty-state__linha" />
          <h1 className="empty-state__titulo">Nenhum resultado encontrado</h1>
          <p className="empty-state__corpo">
            Não há Anúncios que combinem com esses filtros no momento. Ajuste a faixa de preço ou explore outras marcas.
          </p>
          <button type="button" className="cta-button" onClick={aoLimparFiltros}>
            Limpar filtros
          </button>
        </div>
      ) : (
        <>
          <div className="busca-page__sortrow">
            <span className="busca-page__contagem">
              {resultados.length} {resultados.length === 1 ? 'veículo disponível' : 'veículos disponíveis'}
            </span>
            <select
              className="busca-page__ordenacao"
              value={filtros.ordenarPor}
              onChange={(e) => aoAlterarOrdenacao(e.target.value as OrdenacaoBuscaParam)}
              aria-label="Ordenar por"
            >
              <option value="">Relevância</option>
              <option value="preco-asc">Menor preço</option>
              <option value="preco-desc">Maior preço</option>
              <option value="ano-desc">Mais novo</option>
              <option value="ano-asc">Mais antigo</option>
            </select>
          </div>
          <div className="busca-page__lista">
            {resultados.map((anuncio) => (
              <ListingCardBusca key={anuncio.id} anuncio={anuncio} onSelecionar={onSelecionar} />
            ))}
          </div>
          {paginaCheia ? (
            erroCarregarMais ? (
              <div className="busca-page__fim-lista">
                <p className="busca-page__erro" role="alert">
                  {erroCarregarMais}
                </p>
                <button type="button" className="auth-form__alternar" onClick={aoCarregarMais}>
                  Tentar novamente
                </button>
              </div>
            ) : (
              <div className="busca-page__fim-lista">
                <button type="button" className="cta-button" onClick={aoCarregarMais} disabled={carregandoMais}>
                  {carregandoMais ? 'Carregando…' : 'Carregar mais resultados'}
                </button>
              </div>
            )
          ) : (
            <p className="busca-page__fim-lista busca-page__fim-lista--texto">
              Você viu todos os {resultados.length} {resultados.length === 1 ? 'resultado' : 'resultados'}.
            </p>
          )}
        </>
      )}
    </div>
  )
}
