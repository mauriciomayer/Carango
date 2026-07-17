import { useId, useState, type FormEvent } from 'react'
import { AutenticacaoApiError, autenticarVendedor } from './api'
import { salvarToken, salvarTipo } from './authStorage'
import type { TipoVendedor } from './types'
import './AuthForm.css'

type CampoErro = 'email' | 'senha'

interface FormState {
  email: string
  senha: string
}

const estadoInicial: FormState = { email: '', senha: '' }

interface LoginFormProps {
  onAutenticado?: (tipo: TipoVendedor) => void
}

export function LoginForm({ onAutenticado }: LoginFormProps = {}) {
  const [form, setForm] = useState<FormState>(estadoInicial)
  const [errosCampo, setErrosCampo] = useState<Partial<Record<CampoErro, string>>>({})
  const [erroEnvio, setErroEnvio] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [autenticado, setAutenticado] = useState(false)

  const emailId = useId()
  const senhaId = useId()

  function validar(): boolean {
    const erros: Partial<Record<CampoErro, string>> = {}
    if (!form.email.trim()) erros.email = 'Informe seu e-mail.'
    if (!form.senha.trim()) erros.senha = 'Informe sua senha.'
    setErrosCampo(erros)
    return Object.keys(erros).length === 0
  }

  function limparErroCampo(campo: CampoErro) {
    setErrosCampo((atual) => {
      if (!atual[campo]) return atual
      const restante = { ...atual }
      delete restante[campo]
      return restante
    })
  }

  async function aoSubmeter(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErroEnvio(null)

    if (!validar()) return

    setEnviando(true)
    try {
      const resposta = await autenticarVendedor({ email: form.email.trim(), senha: form.senha })
      // lido uma única vez, fora do try de persistência — achado no code review: acessar
      // resposta.vendedor.tipo de novo depois (fora de qualquer try/catch) pra passar pro
      // onAutenticado podia lançar numa resposta malformada, deixando o formulário preso em
      // "Você entrou" (setAutenticado(true) já tinha rodado) sem o App.tsx nunca avançar de tela
      const tipo = resposta.vendedor.tipo

      // salvarToken/salvarTipo separados do try da chamada de API — se localStorage falhar (modo
      // privado, quota excedida), o servidor já autenticou; isso não pode aparecer como "não foi
      // possível entrar". salvarTipo (Story 4.4) segue exatamente o mesmo raciocínio do token
      try {
        salvarToken(resposta.token)
        salvarTipo(tipo)
      } catch {
        // segue como autenticado mesmo assim; a sessão só não sobrevive a um reload nesta aba
      }

      setAutenticado(true)
      onAutenticado?.(tipo)
    } catch (erro) {
      // mensagem genérica vinda do servidor — nunca revela se foi o e-mail ou a senha que errou (AC #2, FR-1)
      const mensagem = erro instanceof AutenticacaoApiError ? erro.message : 'Não foi possível entrar. Tente novamente.'
      setErroEnvio(mensagem)
    } finally {
      setEnviando(false)
    }
  }

  if (autenticado) {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Você entrou</h1>
        <p className="auth-form__corpo">Sessão iniciada com sucesso.</p>
      </div>
    )
  }

  return (
    <form className="auth-form" onSubmit={aoSubmeter} noValidate>
      <h1 className="auth-form__titulo">Entrar</h1>

      <div className="auth-form__campo">
        <label htmlFor={emailId}>E-mail</label>
        <input
          id={emailId}
          type="email"
          autoComplete="email"
          value={form.email}
          onChange={(evento) => {
            setForm((atual) => ({ ...atual, email: evento.target.value }))
            limparErroCampo('email')
          }}
          aria-invalid={Boolean(errosCampo.email)}
          aria-describedby={errosCampo.email ? `${emailId}-erro` : undefined}
        />
        {errosCampo.email && (
          <span id={`${emailId}-erro`} className="auth-form__erro-campo" role="alert">
            {errosCampo.email}
          </span>
        )}
      </div>

      <div className="auth-form__campo">
        <label htmlFor={senhaId}>Senha</label>
        <input
          id={senhaId}
          type="password"
          autoComplete="current-password"
          value={form.senha}
          onChange={(evento) => {
            setForm((atual) => ({ ...atual, senha: evento.target.value }))
            limparErroCampo('senha')
          }}
          aria-invalid={Boolean(errosCampo.senha)}
          aria-describedby={errosCampo.senha ? `${senhaId}-erro` : undefined}
        />
        {errosCampo.senha && (
          <span id={`${senhaId}-erro`} className="auth-form__erro-campo" role="alert">
            {errosCampo.senha}
          </span>
        )}
      </div>

      {/* role="alert" já implica uma live region assertiva implícita — aria-live explícito seria redundante/conflitante */}
      {erroEnvio && (
        <p className="auth-form__erro-envio" role="alert">
          {erroEnvio}
        </p>
      )}

      <button type="submit" className="cta-button" disabled={enviando}>
        {enviando ? 'Entrando…' : 'Entrar'}
      </button>
    </form>
  )
}
