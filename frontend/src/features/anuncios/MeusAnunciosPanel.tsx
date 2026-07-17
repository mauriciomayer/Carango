import { useEffect, useState } from 'react'
import '../autenticacao/AuthForm.css'
import './MeusAnunciosPanel.css'
import { AnuncioApiError, destacarAnuncio, listarMeusAnuncios } from './api'
import type { AnuncioResponse } from './types'
import type { RotuloPainel } from '../autenticacao/types'
import { FORMATADOR_PRECO, metaDoAnuncio } from '../../shared/anuncioFormatacao'

interface MeusAnunciosPanelProps {
  // Story 4.4 — mesmo componente serve "Meus Anúncios" (Pessoa Física) e "Painel do Lojista"
  // (Lojista, EXPERIENCE.md § Fluxos: "substitui Meus Anúncios pra esse perfil"); só o título muda
  titulo?: RotuloPainel
  // Story 4.3 — flag de lógica separada do título: `titulo` é texto de exibição, não deveria
  // carregar significado de autorização/roteamento (achado no code review da Story 4.4)
  ehLojista?: boolean
  onCriar: () => void
  onSelecionar: (anuncioId: string) => void
  onVerBusca: () => void
  onGerenciarAssinatura?: () => void
}

function tituloDoAnuncio(anuncio: AnuncioResponse): string {
  if (!anuncio.marca && !anuncio.modelo) return 'Rascunho sem Ficha preenchida'
  return [anuncio.marca, anuncio.modelo].filter(Boolean).join(' ')
}

// Story 4.1, Task 7 — "Destacar Anúncio" nunca chama a API direto no clique, mesmo padrão de
// confirmação inline já usado na exclusão (Story 2.4): falha mantém a confirmação aberta em vez
// de descartar a intenção do Vendedor. Sem tela de pagamento real (não existe gateway de verdade
// ainda, ver Dev Notes da story) — a "confirmação" É o pagamento nesta story.
function AcaoDestacar({ anuncio, onDestacado }: { anuncio: AnuncioResponse; onDestacado: (atualizado: AnuncioResponse) => void }) {
  const [confirmando, setConfirmando] = useState(false)
  const [destacando, setDestacando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  async function aoConfirmar() {
    setErro(null)
    setDestacando(true)
    try {
      const atualizado = await destacarAnuncio(anuncio.id)
      onDestacado(atualizado)
    } catch (erroCapturado) {
      // mantém confirmando=true — o erro aparece dentro da própria confirmação inline
      const mensagem =
        erroCapturado instanceof AnuncioApiError ? erroCapturado.message : 'Não foi possível destacar o Anúncio. Tente novamente.'
      setErro(mensagem)
      setDestacando(false)
    }
  }

  if (confirmando) {
    return (
      <div className="listing-card__confirmacao" role="alertdialog">
        <p className="listing-card__meta">Destacar este Anúncio?</p>
        {erro && (
          <p className="listing-card__erro" role="alert">
            {erro}
          </p>
        )}
        <button
          type="button"
          className="auth-form__alternar"
          disabled={destacando}
          onClick={() => {
            setConfirmando(false)
            // achado no code review: sem isso, um erro de uma tentativa anterior reaparecia
            // assim que a confirmação era reaberta, antes de qualquer nova tentativa acontecer
            setErro(null)
          }}
        >
          Cancelar
        </button>
        <button type="button" className="cta-button" disabled={destacando} onClick={() => void aoConfirmar()}>
          {destacando ? 'Destacando…' : 'Sim, destacar'}
        </button>
      </div>
    )
  }

  return (
    <button type="button" className="auth-form__alternar" onClick={() => setConfirmando(true)}>
      Destacar Anúncio
    </button>
  )
}

function ListingCard({
  anuncio,
  ehLojista,
  onSelecionar,
  onDestacado,
}: {
  anuncio: AnuncioResponse
  ehLojista: boolean
  onSelecionar: (id: string) => void
  onDestacado: (atualizado: AnuncioResponse) => void
}) {
  const foto = anuncio.fotos[0]?.url
  const meta = metaDoAnuncio(anuncio)
  const classeCard = anuncio.patrocinado ? 'listing-card listing-card--sponsored' : 'listing-card'
  const podeDestacar = anuncio.status === 'Ativo' && !anuncio.patrocinado

  return (
    <div className={classeCard}>
      <button type="button" className="listing-card__area-clicavel" onClick={() => onSelecionar(anuncio.id)}>
        <div className="listing-card__foto-wrapper">
          {foto ? (
            <img className="listing-card__foto" src={foto} alt="" />
          ) : (
            <div className="listing-card__foto" aria-hidden="true" />
          )}
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
          <p className="listing-card__status">{anuncio.status}</p>
          {/* Story 4.5 — métrica só pro Lojista ver sobre o próprio Anúncio, não faz parte de
              metaDoAnuncio (helper compartilhado com BuscaPage/AnuncioDetalhePage, que o Comprador
              não deveria ver). Classe --visualizacoes própria (achado no code review): sem ela,
              um seletor ".listing-card__meta" não distingue esta linha da de metaDoAnuncio acima */}
          {ehLojista && (
            <p className="listing-card__meta listing-card__meta--visualizacoes">
              {anuncio.visualizacoes} {anuncio.visualizacoes === 1 ? 'visualização' : 'visualizações'}
            </p>
          )}
        </div>
      </button>
      {podeDestacar && (
        <div className="listing-card__acoes">
          <AcaoDestacar anuncio={anuncio} onDestacado={onDestacado} />
        </div>
      )}
    </div>
  )
}

// Story 4.4, UX-DR14 — mesma estrutura visual do listing-card (foto + linhas de texto), preenchida
// com blocos --color-paper-2 (mesmo tom já usado como placeholder de foto ausente). Sem
// animação/shimmer — tom calmo do design system, "linha fina, não sombra"; nenhum token de
// skeleton existe em DESIGN.md ainda, decisão de implementação registrada aqui
function ListingCardSkeleton() {
  return (
    <div className="listing-card listing-card--skeleton">
      <div className="listing-card__area-clicavel">
        <div className="listing-card__foto-wrapper">
          <div className="listing-card__foto" />
        </div>
        <div className="listing-card__corpo">
          <div className="listing-card__skeleton-linha listing-card__skeleton-linha--titulo" />
          <div className="listing-card__skeleton-linha listing-card__skeleton-linha--preco" />
          <div className="listing-card__skeleton-linha listing-card__skeleton-linha--meta" />
          {/* achado no code review: o listing-card real sempre tem uma 4ª linha (status), mesmo
              quando preco/meta somem — sem ela o skeleton fica mais baixo que o card real e a
              lista "pula" de altura quando os dados chegam, exatamente o que o skeleton deveria evitar */}
          <div className="listing-card__skeleton-linha listing-card__skeleton-linha--status" />
        </div>
      </div>
    </div>
  )
}

export function MeusAnunciosPanel({
  titulo = 'Meus Anúncios',
  ehLojista = false,
  onCriar,
  onSelecionar,
  onVerBusca,
  onGerenciarAssinatura,
}: MeusAnunciosPanelProps) {
  const [anuncios, setAnuncios] = useState<AnuncioResponse[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [tentativa, setTentativa] = useState(0)

  // guard de desmontagem já é o `cancelado` local do efeito abaixo — só uma operação assíncrona
  // acontece neste componente, então não há necessidade de um useRef separado (diferente do
  // EditarAnuncioForm, que tem várias ações assíncronas concorrentes precisando do mesmo guard)
  useEffect(() => {
    let cancelado = false
    setErro(null)

    async function carregar() {
      try {
        const lista = await listarMeusAnuncios()
        if (!cancelado) setAnuncios(lista)
      } catch (erroCapturado) {
        if (cancelado) return
        const mensagem =
          erroCapturado instanceof AnuncioApiError
            ? erroCapturado.message
            : 'Não foi possível carregar seus Anúncios. Tente novamente.'
        setErro(mensagem)
      }
    }

    void carregar()
    return () => {
      cancelado = true
    }
  }, [tentativa])

  function aoDestacar(atualizado: AnuncioResponse) {
    setAnuncios((lista) => lista?.map((anuncio) => (anuncio.id === atualizado.id ? atualizado : anuncio)) ?? lista)
  }

  if (erro) {
    return (
      <div className="meus-anuncios">
        <p className="meus-anuncios__erro" role="alert">
          {erro}
        </p>
        <button type="button" className="auth-form__alternar" onClick={() => setTentativa((n) => n + 1)}>
          Tentar novamente
        </button>
      </div>
    )
  }

  if (anuncios === null) {
    return (
      <div className="meus-anuncios" role="status">
        {/* achado no code review: antes o texto "Carregando…" era o próprio conteúdo acessível
            da região role="status" — o skeleton visual é todo aria-hidden, então sem isso um
            leitor de tela não anuncia nada enquanto a lista carrega */}
        <span className="sr-only">Carregando…</span>
        <div className="meus-anuncios__lista" aria-hidden="true">
          {[0, 1, 2].map((indice) => (
            <ListingCardSkeleton key={indice} />
          ))}
        </div>
      </div>
    )
  }

  if (anuncios.length === 0) {
    return (
      <div className="meus-anuncios">
        <div className="empty-state">
          <hr className="empty-state__linha" />
          <h1 className="empty-state__titulo">Nenhum Anúncio ainda</h1>
          <p className="empty-state__corpo">Publique seu primeiro veículo e acompanhe tudo por aqui.</p>
          <button type="button" className="cta-button" onClick={onCriar}>
            Criar meu primeiro Anúncio
          </button>
        </div>
        {/* achado no code review (Story 4.3): um Lojista com Plano ativo mas zero Anúncios não
            tinha nenhum jeito de chegar em "Gerenciar Assinatura" a partir daqui */}
        {ehLojista && onGerenciarAssinatura && (
          <button type="button" className="auth-form__alternar" onClick={onGerenciarAssinatura}>
            Gerenciar Assinatura
          </button>
        )}
      </div>
    )
  }

  return (
    <div className="meus-anuncios">
      <div className="meus-anuncios__cabecalho">
        <h1 className="meus-anuncios__titulo">{titulo}</h1>
        {/* achado no code review: o link ficava como irmão do cabeçalho, não dentro dele como o
            texto da story descrevia — agrupado com "Ver Busca" pra manter o cabeçalho com
            exatamente 2 filhos flex (título + grupo de ações), preservando o space-between */}
        <div className="meus-anuncios__acoes-cabecalho">
          {ehLojista && onGerenciarAssinatura && (
            <button type="button" className="auth-form__alternar" onClick={onGerenciarAssinatura}>
              Gerenciar Assinatura
            </button>
          )}
          <button type="button" className="auth-form__alternar" onClick={onVerBusca}>
            Ver Busca
          </button>
        </div>
      </div>
      <button type="button" className="cta-button" onClick={onCriar}>
        Criar novo Anúncio
      </button>
      <div className="meus-anuncios__lista">
        {anuncios.map((anuncio) => (
          <ListingCard
            key={anuncio.id}
            anuncio={anuncio}
            ehLojista={ehLojista}
            onSelecionar={onSelecionar}
            onDestacado={aoDestacar}
          />
        ))}
      </div>
    </div>
  )
}
