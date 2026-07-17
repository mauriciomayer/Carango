import { useEffect, useState } from 'react'
import '../autenticacao/AuthForm.css'
import './BuscaPage.css'
import './AnuncioDetalhePage.css'
import { AnuncioDetalheApiError, obterDetalheAnuncio } from './api'
import type { AnuncioDetalheResponse } from './types'
import { FORMATADOR_PRECO, metaDoAnuncio } from '../../shared/anuncioFormatacao'

interface AnuncioDetalhePageProps {
  anuncioId: string
  onVoltar: () => void
}

function Galeria({ fotos }: { fotos: string[] }) {
  const [fotoAtual, setFotoAtual] = useState(0)

  if (fotos.length === 0) {
    return <div className="anuncio-detalhe__foto" aria-hidden="true" />
  }

  const temNavegacao = fotos.length > 1

  return (
    <div className="anuncio-detalhe__galeria">
      {/* achado no code review: alt="" tirava toda informação da foto principal (diferente das
          miniaturas da listagem, que são só decorativas ao lado de um título já visível) */}
      <img className="anuncio-detalhe__foto" src={fotos[fotoAtual]} alt={`Foto ${fotoAtual + 1} de ${fotos.length} do Anúncio`} />
      {temNavegacao && (
        <>
          <button
            type="button"
            className="anuncio-detalhe__galeria-botao anuncio-detalhe__galeria-botao--anterior"
            onClick={() => setFotoAtual((atual) => (atual === 0 ? fotos.length - 1 : atual - 1))}
            aria-label="Foto anterior"
          >
            ‹
          </button>
          <button
            type="button"
            className="anuncio-detalhe__galeria-botao anuncio-detalhe__galeria-botao--proxima"
            onClick={() => setFotoAtual((atual) => (atual === fotos.length - 1 ? 0 : atual + 1))}
            aria-label="Próxima foto"
          >
            ›
          </button>
          <span className="anuncio-detalhe__galeria-contador">
            Foto {fotoAtual + 1} de {fotos.length}
          </span>
        </>
      )}
    </div>
  )
}

export function AnuncioDetalhePage({ anuncioId, onVoltar }: AnuncioDetalhePageProps) {
  const [anuncio, setAnuncio] = useState<AnuncioDetalheResponse | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [erroNaoEncontrado, setErroNaoEncontrado] = useState(false)
  const [tentativa, setTentativa] = useState(0)

  useEffect(() => {
    let cancelado = false
    setErro(null)
    setAnuncio(null)

    async function carregar() {
      try {
        const resultado = await obterDetalheAnuncio(anuncioId)
        if (!cancelado) setAnuncio(resultado)
      } catch (erroCapturado) {
        if (cancelado) return
        const naoEncontrado = erroCapturado instanceof AnuncioDetalheApiError && erroCapturado.naoEncontrado
        const mensagem =
          erroCapturado instanceof AnuncioDetalheApiError
            ? erroCapturado.message
            : 'Não foi possível carregar os dados do Anúncio. Tente novamente.'
        setErroNaoEncontrado(naoEncontrado)
        setErro(mensagem)
      }
    }

    void carregar()
    return () => {
      cancelado = true
    }
  }, [anuncioId, tentativa])

  if (erro) {
    return (
      <div className="anuncio-detalhe anuncio-detalhe--estado">
        <p className="busca-page__erro" role="alert">
          {erro}
        </p>
        {erroNaoEncontrado ? (
          <button type="button" className="cta-button" onClick={onVoltar}>
            Voltar à Busca
          </button>
        ) : (
          <button type="button" className="auth-form__alternar" onClick={() => setTentativa((n) => n + 1)}>
            Tentar novamente
          </button>
        )}
      </div>
    )
  }

  if (anuncio === null) {
    return (
      <div className="anuncio-detalhe anuncio-detalhe--estado" role="status">
        <p className="auth-form__corpo">Carregando…</p>
      </div>
    )
  }

  const meta = metaDoAnuncio(anuncio)

  return (
    <div className="anuncio-detalhe">
      <div className="anuncio-detalhe__conteudo">
        <button type="button" className="anuncio-detalhe__voltar auth-form__alternar" onClick={onVoltar}>
          ‹ Voltar à Busca
        </button>
        <Galeria fotos={anuncio.fotos} />
        <div className="anuncio-detalhe__corpo">
          <h1 className="anuncio-detalhe__titulo">
            {anuncio.marca} {anuncio.modelo}
          </h1>
          {anuncio.preco != null && (
            <p className="anuncio-detalhe__preco">
              <strong>{FORMATADOR_PRECO.format(anuncio.preco)}</strong>
            </p>
          )}
          {meta && <p className="anuncio-detalhe__meta">{meta}</p>}
          {anuncio.descricao && <p className="anuncio-detalhe__descricao">{anuncio.descricao}</p>}
        </div>
      </div>
    </div>
  )
}
