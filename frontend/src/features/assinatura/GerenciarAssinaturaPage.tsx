import { useEffect, useRef, useState } from 'react'
import '../autenticacao/AuthForm.css'
import { AssinaturaApiError, cancelarPlanoLojista, obterMeuPlano } from './api'
import type { PlanoLojistaResponse } from './types'

interface GerenciarAssinaturaPageProps {
  onVoltar?: () => void
}

// Story 4.3 — sem mockup de alta-fidelidade específico (mesma situação de MeusAnunciosPanel/
// CriarAnuncioForm), construído a partir dos tokens, mobile-first. Só cancelamento nesta story
// (upgrade de nível de plano fica pendente de decisão de produto, ver Dev Notes da story)
export function GerenciarAssinaturaPage({ onVoltar }: GerenciarAssinaturaPageProps = {}) {
  // undefined = ainda não carregou; null = carregou e não há Plano Lojista (nunca assinou ou já cancelado)
  const [plano, setPlano] = useState<PlanoLojistaResponse | null | undefined>(undefined)
  const [erroCarregar, setErroCarregar] = useState<string | null>(null)
  const [tentativa, setTentativa] = useState(0)
  const [confirmando, setConfirmando] = useState(false)
  const [cancelando, setCancelando] = useState(false)
  const [erroCancelar, setErroCancelar] = useState<string | null>(null)
  const [cancelado, setCancelado] = useState(false)
  const montado = useRef(true)

  useEffect(() => {
    montado.current = true
    return () => {
      montado.current = false
    }
  }, [])

  useEffect(() => {
    let ignorar = false
    setErroCarregar(null)

    async function carregar() {
      try {
        const resultado = await obterMeuPlano()
        if (!ignorar) setPlano(resultado)
      } catch (erro) {
        if (ignorar) return
        const mensagem =
          erro instanceof AssinaturaApiError ? erro.message : 'Não foi possível carregar sua assinatura. Tente novamente.'
        setErroCarregar(mensagem)
      }
    }

    void carregar()
    return () => {
      ignorar = true
    }
  }, [tentativa])

  // AC #1: cancelar nunca chama a API direto no clique — confirmação inline (nunca window.confirm()
  // nativo, tom calmo/editorial), mesmo padrão já usado na exclusão de Anúncio (Story 2.4) e no
  // Destacar (Story 4.1): falha mantém a confirmação aberta com o erro dentro dela
  async function aoConfirmarCancelamento() {
    setErroCancelar(null)
    setCancelando(true)
    try {
      await cancelarPlanoLojista()
      if (!montado.current) return
      setCancelado(true)
    } catch (erro) {
      if (!montado.current) return
      const mensagem = erro instanceof AssinaturaApiError ? erro.message : 'Não foi possível cancelar o Plano Lojista. Tente novamente.'
      setErroCancelar(mensagem)
    } finally {
      if (montado.current) setCancelando(false)
    }
  }

  // erroCarregar checado antes de "plano === undefined" — depois de uma falha, plano nunca deixa
  // de ser undefined (não seteamos plano no catch), então checar a ordem inversa mostraria
  // "Carregando…" pra sempre em vez do erro
  if (erroCarregar) {
    return (
      <div className="auth-form" role="alert">
        <p className="auth-form__erro-envio">{erroCarregar}</p>
        <button type="button" className="auth-form__alternar" onClick={() => setTentativa((n) => n + 1)}>
          Tentar novamente
        </button>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar
          </button>
        )}
      </div>
    )
  }

  if (plano === undefined) {
    return (
      <div className="auth-form" role="status">
        <p className="auth-form__corpo">Carregando…</p>
      </div>
    )
  }

  if (cancelado) {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Plano cancelado</h1>
        {/* achado no code review: "voltou ao padrão de Pessoa Física" sugeria que o Tipo da conta
            mudava — o Vendedor continua Lojista pra sempre, só o limite de Anúncios ativos volta
            a valer (a isenção da Story 4.2 é o que some, não o Tipo) */}
        <p className="auth-form__corpo">Você volta a ficar limitado a 1 Anúncio ativo por vez.</p>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar
          </button>
        )}
      </div>
    )
  }

  // AC #3: sem plano ativo (nunca assinou, ou já está Cancelado) — estado refletido claramente,
  // sem erro nem tela quebrada. Sem CTA de assinar aqui de propósito (fora de escopo desta story,
  // ver Dev Notes — a Story 4.2 também não construiu essa UI)
  if (plano === null || plano.status === 'Cancelado') {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Gerenciar Assinatura</h1>
        <p className="auth-form__corpo">Você não tem um Plano Lojista ativo.</p>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar
          </button>
        )}
      </div>
    )
  }

  if (confirmando) {
    return (
      <div className="auth-form" role="alertdialog">
        <h1 className="auth-form__titulo">Cancelar seu Plano Lojista?</h1>
        <p className="auth-form__corpo">Você volta a ficar limitado a 1 Anúncio ativo por vez.</p>
        {erroCancelar && (
          <p className="auth-form__erro-envio" role="alert">
            {erroCancelar}
          </p>
        )}
        <button type="button" className="auth-form__alternar" disabled={cancelando} onClick={() => setConfirmando(false)}>
          Voltar
        </button>
        <button type="button" className="cta-button" disabled={cancelando} onClick={() => void aoConfirmarCancelamento()}>
          {cancelando ? 'Cancelando…' : 'Sim, cancelar'}
        </button>
      </div>
    )
  }

  return (
    <div className="auth-form">
      {onVoltar && (
        <button type="button" className="auth-form__alternar" onClick={onVoltar}>
          ← Voltar
        </button>
      )}
      <h1 className="auth-form__titulo">Gerenciar Assinatura</h1>
      <p className="auth-form__corpo">Seu Plano Lojista está ativo.</p>
      <button type="button" className="auth-form__alternar" onClick={() => setConfirmando(true)}>
        Cancelar Plano
      </button>
    </div>
  )
}
