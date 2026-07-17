import { useState } from 'react'
import { obterToken, obterTipo } from './features/autenticacao/authStorage'
import { AutenticacaoPage } from './features/autenticacao/AutenticacaoPage'
import { rotuloPainel } from './features/autenticacao/types'
import type { TipoVendedor } from './features/autenticacao/types'
import { AnuncioDetalhePage } from './features/busca/AnuncioDetalhePage'
import { BuscaPage } from './features/busca/BuscaPage'
import { CriarAnuncioForm } from './features/anuncios/CriarAnuncioForm'
import { EditarAnuncioForm } from './features/anuncios/EditarAnuncioForm'
import { MeusAnunciosPanel } from './features/anuncios/MeusAnunciosPanel'
import { GerenciarAssinaturaPage } from './features/assinatura/GerenciarAssinaturaPage'

type Tela = 'busca' | 'autenticacao' | 'painel' | 'criar' | 'assinatura'

// navegação mínima sem router. A partir da Story 3.1, Busca é a tela padrão pra TODO mundo,
// autenticado ou não — não existe conta de Comprador em nenhum épico, então a busca pública
// tem que ser alcançável sem login (o mockup hero-search.html mostra um avatar no header,
// implicando que um Vendedor autenticado também vê essa mesma tela). Vendedor autenticado
// chega no painel (Story 2.5) via um link "Meus Anúncios" no header da Busca; visitante chega
// em Login/Cadastro via um link "Entrar" — login bem-sucedido continua indo pro painel,
// mesmo comportamento já existente desde a Epic 2
function App() {
  const [autenticado, setAutenticado] = useState(() => obterToken() !== null)
  // Story 4.4 — mesmo padrão de autenticado/obterToken(): sobrevive a reload, não só à sessão da aba
  const [tipoVendedor, setTipoVendedor] = useState<TipoVendedor | null>(() => obterTipo())
  const [tela, setTela] = useState<Tela>('busca')
  const [anuncioEmEdicao, setAnuncioEmEdicao] = useState<string | null>(null)
  // Detalhe público (Story 3.5) — estado distinto de `anuncioEmEdicao` (que é o fluxo owner-only
  // de editar). Renderizado como overlay dentro do branch da Busca (ver abaixo), nunca setado
  // fora dele — Detalhe é alcançável tanto por visitante quanto por Vendedor autenticado, igual
  // à própria Busca
  const [anuncioDetalheId, setAnuncioDetalheId] = useState<string | null>(null)
  // Story 4.4, achado no code review — fonte única do rótulo, calculada uma vez e passada pra
  // todo mundo que precisa dela (título do painel, link da Busca, links "Voltar aos..." de
  // Criar/Editar Anúncio), em vez de cada lugar computar seu próprio ternário independente
  const tituloPainel = rotuloPainel(tipoVendedor)
  // Story 4.3 — flag de lógica separada do rótulo de exibição (achado no code review da Story 4.4:
  // não reaproveitar tituloPainel === 'Painel do Lojista' como condição de autorização/roteamento)
  const ehLojista = tipoVendedor === 'Lojista'

  if (tela === 'autenticacao' && !autenticado) {
    return (
      <AutenticacaoPage
        onAutenticado={(tipo) => {
          setAutenticado(true)
          setTipoVendedor(tipo)
          setTela('painel')
        }}
      />
    )
  }

  if (!autenticado || tela === 'busca') {
    // AnuncioDetalhePage é renderizado como um overlay POR CIMA da BuscaPage, não em vez dela —
    // achado no code review: a versão anterior desmontava a BuscaPage inteira ao abrir um
    // detalhe, perdendo silenciosamente filtros/ordenação/páginas já carregadas ao voltar.
    // `onSelecionar` só pode vir da própria BuscaPage renderizada aqui, então é seguro aninhar
    // o overlay dentro deste branch (nunca é setado enquanto painel/criar/editar estão visíveis)
    return (
      <>
        <BuscaPage
          autenticado={autenticado}
          tituloPainel={tituloPainel}
          onEntrar={() => setTela('autenticacao')}
          onMeusAnuncios={() => setTela('painel')}
          onSelecionar={setAnuncioDetalheId}
        />
        {anuncioDetalheId && (
          <AnuncioDetalhePage anuncioId={anuncioDetalheId} onVoltar={() => setAnuncioDetalheId(null)} />
        )}
      </>
    )
  }

  if (anuncioEmEdicao) {
    const voltarAoPainel = () => {
      setAnuncioEmEdicao(null)
      setTela('painel')
    }
    // onExcluido e onVoltar são destinos DIFERENTES de propósito: "Voltar aos Meus Anúncios" volta
    // pro painel, "Criar novo Anúncio" (rótulo do botão na tela de exclusão) abre o formulário de
    // criação — achado no code review, os dois estavam apontando pro mesmo lugar
    const aoExcluir = () => {
      setAnuncioEmEdicao(null)
      setTela('criar')
    }
    return (
      <EditarAnuncioForm
        anuncioId={anuncioEmEdicao}
        tituloVoltar={tituloPainel}
        onExcluido={aoExcluir}
        onVoltar={voltarAoPainel}
      />
    )
  }

  if (tela === 'criar') {
    return (
      <CriarAnuncioForm tituloVoltar={tituloPainel} onEditar={setAnuncioEmEdicao} onVoltar={() => setTela('painel')} />
    )
  }

  if (tela === 'assinatura') {
    return <GerenciarAssinaturaPage onVoltar={() => setTela('painel')} />
  }

  return (
    <MeusAnunciosPanel
      titulo={tituloPainel}
      ehLojista={ehLojista}
      onCriar={() => setTela('criar')}
      onSelecionar={setAnuncioEmEdicao}
      onVerBusca={() => setTela('busca')}
      onGerenciarAssinatura={() => setTela('assinatura')}
    />
  )
}

export default App
