import { useId, useState, type FormEvent } from 'react'
import { AutenticacaoApiError, cadastrarVendedor } from './api'
import type { TipoVendedor } from './types'
import './AuthForm.css'

type CampoErro = 'email' | 'senha' | 'cnpjRazaoSocial'

interface FormState {
  tipo: TipoVendedor
  email: string
  senha: string
  cnpjRazaoSocial: string
}

const estadoInicial: FormState = {
  tipo: 'PessoaFisica',
  email: '',
  senha: '',
  cnpjRazaoSocial: '',
}

interface CadastroFormProps {
  onCadastroConcluido?: () => void
}

export function CadastroForm({ onCadastroConcluido }: CadastroFormProps = {}) {
  const [form, setForm] = useState<FormState>(estadoInicial)
  const [errosCampo, setErrosCampo] = useState<Partial<Record<CampoErro, string>>>({})
  const [erroEnvio, setErroEnvio] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [concluido, setConcluido] = useState(false)

  const emailId = useId()
  const senhaId = useId()
  const cnpjId = useId()

  function validar(): boolean {
    const erros: Partial<Record<CampoErro, string>> = {}
    if (!form.email.trim()) erros.email = 'Informe seu e-mail.'
    if (!form.senha.trim()) erros.senha = 'Informe uma senha.'
    if (form.tipo === 'Lojista' && !form.cnpjRazaoSocial.trim()) {
      erros.cnpjRazaoSocial = 'CNPJ/razão social é obrigatório para conta Lojista.'
    }
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
      await cadastrarVendedor({
        email: form.email.trim(),
        senha: form.senha,
        tipo: form.tipo,
        cnpjRazaoSocial: form.tipo === 'Lojista' ? form.cnpjRazaoSocial.trim() : undefined,
      })
      setConcluido(true)
    } catch (erro) {
      const mensagem =
        erro instanceof AutenticacaoApiError ? erro.message : 'Não foi possível concluir o cadastro. Tente novamente.'
      setErroEnvio(mensagem)
    } finally {
      setEnviando(false)
    }
  }

  if (concluido) {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Conta criada</h1>
        <p className="auth-form__corpo">Sua conta foi criada. Você já pode entrar com o e-mail e a senha cadastrados.</p>
        {onCadastroConcluido && (
          <button type="button" className="cta-button" onClick={onCadastroConcluido}>
            Entrar agora
          </button>
        )}
      </div>
    )
  }

  return (
    <form className="auth-form" onSubmit={aoSubmeter} noValidate>
      <h1 className="auth-form__titulo">Criar conta</h1>

      <fieldset className="auth-form__tipo">
        <legend className="auth-form__micro-label">Tipo de conta</legend>
        <label className="auth-form__opcao">
          <input
            type="radio"
            name="tipo"
            value="PessoaFisica"
            checked={form.tipo === 'PessoaFisica'}
            onChange={() => {
              setForm((atual) => ({ ...atual, tipo: 'PessoaFisica' }))
              limparErroCampo('cnpjRazaoSocial')
            }}
          />
          Pessoa Física
        </label>
        <label className="auth-form__opcao">
          <input
            type="radio"
            name="tipo"
            value="Lojista"
            checked={form.tipo === 'Lojista'}
            onChange={() => setForm((atual) => ({ ...atual, tipo: 'Lojista' }))}
          />
          Lojista
        </label>
      </fieldset>

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
          autoComplete="new-password"
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

      {form.tipo === 'Lojista' && (
        <div className="auth-form__campo">
          <label htmlFor={cnpjId}>CNPJ / razão social</label>
          <input
            id={cnpjId}
            type="text"
            value={form.cnpjRazaoSocial}
            onChange={(evento) => {
              setForm((atual) => ({ ...atual, cnpjRazaoSocial: evento.target.value }))
              limparErroCampo('cnpjRazaoSocial')
            }}
            aria-invalid={Boolean(errosCampo.cnpjRazaoSocial)}
            aria-describedby={errosCampo.cnpjRazaoSocial ? `${cnpjId}-erro` : undefined}
          />
          {errosCampo.cnpjRazaoSocial && (
            <span id={`${cnpjId}-erro`} className="auth-form__erro-campo" role="alert">
              {errosCampo.cnpjRazaoSocial}
            </span>
          )}
        </div>
      )}

      {/* role="alert" já implica uma live region assertiva implícita — aria-live explícito seria redundante/conflitante */}
      {erroEnvio && (
        <p className="auth-form__erro-envio" role="alert">
          {erroEnvio}
        </p>
      )}

      <button type="submit" className="cta-button" disabled={enviando}>
        {enviando ? 'Enviando…' : 'Criar conta'}
      </button>
    </form>
  )
}
