import { useState } from 'react'
import { CadastroForm } from './CadastroForm'
import { LoginForm } from './LoginForm'
import type { TipoVendedor } from './types'
import './AuthForm.css'

type Modo = 'login' | 'cadastro'

interface AutenticacaoPageProps {
  onAutenticado?: (tipo: TipoVendedor) => void
}

export function AutenticacaoPage({ onAutenticado }: AutenticacaoPageProps = {}) {
  // Login como modo padrão — quem já tem conta é o caso mais comum numa tela combinada de entrada
  const [modo, setModo] = useState<Modo>('login')

  return (
    <div>
      {modo === 'login' ? (
        <LoginForm onAutenticado={onAutenticado} />
      ) : (
        <CadastroForm onCadastroConcluido={() => setModo('login')} />
      )}

      <div className="auth-form__rodape">
        {modo === 'login' ? (
          <button type="button" className="auth-form__alternar" onClick={() => setModo('cadastro')}>
            Não tem conta? <strong>Cadastre-se</strong>
          </button>
        ) : (
          <button type="button" className="auth-form__alternar" onClick={() => setModo('login')}>
            Já tem conta? <strong>Entrar</strong>
          </button>
        )}
      </div>
    </div>
  )
}
