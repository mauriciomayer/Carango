import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import '../autenticacao/AuthForm.css'
import {
  AnuncioApiError, adicionarFotosAnuncio, editarAnuncio, excluirAnuncio, marcarAnuncioVendido,
  obterAnuncio, pausarAnuncio, reativarAnuncio, removerFotoAnuncio,
} from './api'
import type { AnuncioResponse, FotoResponse, StatusAnuncio } from './types'
import type { RotuloPainel } from '../autenticacao/types'
import { ComboboxCascata, type OpcaoCombobox } from '../../shared/ComboboxCascata'
import { VeiculoReferenciaApiError, listarMarcasFipe, listarModelosFipe } from '../../shared/veiculoReferenciaApi'
import { listarEstados, listarMunicipios } from '../../shared/ibgeLocalidades'

type CampoErro = 'marca' | 'modelo' | 'ano' | 'versao' | 'preco' | 'descricao' | 'estado' | 'cidade'

interface FormState {
  marca: string
  modelo: string
  ano: string
  versao: string
  preco: string
  descricao: string
  estado: string
  cidade: string
}

const estadoVazio: FormState = {
  marca: '',
  modelo: '',
  ano: '',
  versao: '',
  preco: '',
  descricao: '',
  estado: '',
  cidade: '',
}

// Marca/Modelo (Story 2.6) e Estado/Cidade (Story 2.7) saíram deste array genérico — viram
// ComboboxCascata, renderizado explicitamente antes deste map. Descrição saiu também (achado em
// teste manual do usuário — precisa de <textarea>, multi-linha, não cabe no <input> genérico
// abaixo). Os 3 campos restantes continuam texto/número livre
const CAMPOS: ReadonlyArray<readonly [Exclude<keyof FormState, 'marca' | 'modelo' | 'estado' | 'cidade' | 'descricao'>, string, 'text' | 'number']> = [
  ['ano', 'Ano', 'number'],
  ['versao', 'Versão', 'text'],
  ['preco', 'Preço', 'number'],
]

// mesmo piso já usado no filtro de busca (BuscaPage.tsx, ANO_MINIMO) — a garantia real vive no
// Domain (Anuncio.ValidarCamposObrigatorios); isto aqui é só UX (falha rápido, antes do round-trip)
const ANO_MINIMO = 1990

interface EditarAnuncioFormProps {
  anuncioId: string
  // Story 4.4, achado no code review — mesmo raciocínio de CriarAnuncioForm: o rótulo do link
  // "Voltar aos..." tinha "Meus Anúncios" hardcoded, divergindo do "Painel do Lojista"
  tituloVoltar?: RotuloPainel
  onExcluido?: () => void
  onVoltar?: () => void
}

export function EditarAnuncioForm({ anuncioId, tituloVoltar = 'Meus Anúncios', onExcluido, onVoltar }: EditarAnuncioFormProps) {
  const [form, setForm] = useState<FormState>(estadoVazio)
  const [status, setStatus] = useState<StatusAnuncio | null>(null)
  const [carregando, setCarregando] = useState(true)
  const [erroCarregar, setErroCarregar] = useState<string | null>(null)
  const [tentativa, setTentativa] = useState(0)
  const [errosCampo, setErrosCampo] = useState<Partial<Record<CampoErro, string>>>({})
  const [erroEnvio, setErroEnvio] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [salvo, setSalvo] = useState(false)
  const [transicionando, setTransicionando] = useState(false)
  const [acaoStatusConcluida, setAcaoStatusConcluida] = useState<'pausado' | 'reativado' | 'vendido' | null>(null)
  const [confirmandoExclusao, setConfirmandoExclusao] = useState(false)
  const [excluindo, setExcluindo] = useState(false)
  const [excluido, setExcluido] = useState(false)
  const montado = useRef(true)

  // Story 2.8 — fotos de um Anúncio já existente. Ações independentes do "Salvar" principal (mesmo
  // espírito de Pausar/Reativar/Marcar como vendido), porque adicionar/remover foto é multipart/DELETE,
  // não cabe no mesmo corpo JSON de editarAnuncio
  const [fotos, setFotos] = useState<FotoResponse[]>([])
  const [novasFotos, setNovasFotos] = useState<File[]>([])
  const [enviandoFotos, setEnviandoFotos] = useState(false)
  const [erroFotos, setErroFotos] = useState<string | null>(null)
  const [fotoParaRemover, setFotoParaRemover] = useState<string | null>(null)
  const inputFotosRef = useRef<HTMLInputElement>(null)
  const [removendoFoto, setRemovendoFoto] = useState(false)

  // Story 2.6 — Marca/Modelo via Fipe. O Anúncio só guarda o NOME salvo (marca/modelo são
  // string?, não código Fipe), então ao carregar pra edição é preciso resolver o código da
  // Marca atual procurando por nome na lista já carregada — ver Dev Notes/Task 6 da story pro
  // caso de borda de Anúncios antigos cuja Marca não bate com nenhum nome da Fipe
  const [marcas, setMarcas] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroMarcas, setErroMarcas] = useState<string | null>(null)
  const [tentativaMarcas, setTentativaMarcas] = useState(0)
  const [marcaCodigo, setMarcaCodigo] = useState<string | null>(null)
  const [modelos, setModelos] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroModelos, setErroModelos] = useState<string | null>(null)
  const [tentativaModelos, setTentativaModelos] = useState(0)
  const resolveuMarcaInicial = useRef(false)

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
        // achado no code review: sem setar marcas aqui, opcoes fica undefined pra sempre após
        // uma falha, e o ComboboxCascata mostra "Carregando…" e a mensagem de erro ao mesmo tempo
        setMarcas([])
        setErroMarcas(erro instanceof VeiculoReferenciaApiError ? erro.message : 'Não foi possível carregar as marcas. Tente novamente.')
      })
    return () => {
      cancelado = true
    }
  }, [tentativaMarcas])

  // resolução de código-por-nome, uma única vez, assim que o Anúncio e as marcas terminam de
  // carregar — não repete depois disso; uma nova Marca escolhida pelo Vendedor passa a definir
  // marcaCodigo diretamente via aoSelecionarMarca
  useEffect(() => {
    if (resolveuMarcaInicial.current) return
    if (carregando || marcas === undefined) return
    resolveuMarcaInicial.current = true
    if (!form.marca) return
    const combina = marcas.find((m) => m.nome.toLowerCase() === form.marca.toLowerCase())
    if (combina) setMarcaCodigo(combina.codigo)
  }, [carregando, marcas, form.marca])

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

  function aoSelecionarMarca(opcao: OpcaoCombobox) {
    setForm((atual) => ({ ...atual, marca: opcao.nome, modelo: '' }))
    limparErroCampo('marca')
    limparErroCampo('modelo')
    setMarcaCodigo(opcao.codigo)
  }

  function aoSelecionarModelo(opcao: OpcaoCombobox) {
    setForm((atual) => ({ ...atual, modelo: opcao.nome }))
    limparErroCampo('modelo')
  }

  // Story 2.7 — Estado/Cidade via dataset estático do IBGE. O Anúncio guarda a SIGLA do Estado
  // (não o nome), então a resolução aqui é mais simples que a de Marca: compara form.estado
  // direto contra o codigo (sigla) de cada item, uma única vez, mesmo padrão de resolveuMarcaInicial.
  // Achado no code review: "estático" não é "sem chamada de rede" — mesmo tratamento de
  // erro/retry de Marca/Modelo, não a ausência dele
  const [estados, setEstados] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroEstados, setErroEstados] = useState<string | null>(null)
  const [tentativaEstados, setTentativaEstados] = useState(0)
  const [estadoSigla, setEstadoSigla] = useState<string | null>(null)
  const [municipios, setMunicipios] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroMunicipios, setErroMunicipios] = useState<string | null>(null)
  const [tentativaMunicipios, setTentativaMunicipios] = useState(0)
  const resolveuEstadoInicial = useRef(false)

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
    if (resolveuEstadoInicial.current) return
    if (carregando || estados === undefined) return
    resolveuEstadoInicial.current = true
    if (!form.estado) return
    const combina = estados.find((e) => e.codigo.toLowerCase() === form.estado.toLowerCase())
    if (combina) setEstadoSigla(combina.codigo)
  }, [carregando, estados, form.estado])

  useEffect(() => {
    if (!estadoSigla) return
    let cancelado = false
    setErroMunicipios(null)
    setMunicipios(undefined)
    listarMunicipios(estadoSigla)
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
  }, [estadoSigla, tentativaMunicipios])

  function aoSelecionarEstado(opcao: OpcaoCombobox) {
    setForm((atual) => ({ ...atual, estado: opcao.codigo, cidade: '' }))
    limparErroCampo('estado')
    limparErroCampo('cidade')
    setEstadoSigla(opcao.codigo)
  }

  function aoSelecionarCidade(opcao: OpcaoCombobox) {
    setForm((atual) => ({ ...atual, cidade: opcao.nome }))
    limparErroCampo('cidade')
  }

  const marcaId = useId()
  const modeloId = useId()
  const anoId = useId()
  const versaoId = useId()
  const precoId = useId()
  const descricaoId = useId()
  const estadoId = useId()
  const cidadeId = useId()
  const fotosId = useId()

  const idsPorCampo: Record<Exclude<keyof FormState, 'marca' | 'modelo' | 'estado' | 'cidade'>, string> = {
    ano: anoId,
    versao: versaoId,
    preco: precoId,
    descricao: descricaoId,
  }

  useEffect(() => {
    montado.current = true
    return () => {
      montado.current = false
    }
  }, [])

  useEffect(() => {
    let cancelado = false
    setCarregando(true)
    setErroCarregar(null)
    // achado no code review: sem isso, trocar de anuncioId num componente já montado (sem
    // desmontar) deixaria a resolução de código-por-nome travada no resultado do Anúncio anterior
    resolveuMarcaInicial.current = false
    setMarcaCodigo(null)
    resolveuEstadoInicial.current = false
    setEstadoSigla(null)
    // achado no code review: o reset acima limpava marcaCodigo/estadoSigla mas não os
    // dependentes modelos/municipios — mascarado só porque os campos ficam desabilitados nesse
    // instante, não uma limpeza de verdade (achado originalmente só pra municipios, mas o mesmo
    // gap existia pra modelos — corrigidos os dois juntos aqui)
    setModelos(undefined)
    setMunicipios(undefined)

    async function carregar() {
      try {
        const anuncio = await obterAnuncio(anuncioId)
        if (cancelado) return
        setStatus(anuncio.status)
        setFotos(anuncio.fotos)
        setForm({
          marca: anuncio.marca ?? '',
          modelo: anuncio.modelo ?? '',
          ano: anuncio.ano?.toString() ?? '',
          versao: anuncio.versao ?? '',
          preco: anuncio.preco?.toString() ?? '',
          descricao: anuncio.descricao ?? '',
          estado: anuncio.estado ?? '',
          cidade: anuncio.cidade ?? '',
        })
      } catch (erro) {
        if (cancelado) return
        const mensagem =
          erro instanceof AnuncioApiError ? erro.message : 'Não foi possível carregar o Anúncio. Tente novamente.'
        setErroCarregar(mensagem)
      } finally {
        if (!cancelado) setCarregando(false)
      }
    }

    void carregar()
    return () => {
      cancelado = true
    }
  }, [anuncioId, tentativa])

  function limparErroCampo(campo: CampoErro) {
    setErrosCampo((atual) => {
      if (!atual[campo]) return atual
      const restante = { ...atual }
      delete restante[campo]
      return restante
    })
  }

  function atualizarCampo(campo: keyof FormState, valor: string) {
    setForm((atual) => ({ ...atual, [campo]: valor }))
    limparErroCampo(campo)
  }

  // Story 2.8 — ação imediata e independente do "Salvar" principal (mesmo espírito de
  // Pausar/Reativar/Marcar como vendido); a resposta já vem com a lista completa e atualizada de fotos
  async function aoAdicionarFotos() {
    if (novasFotos.length === 0) return
    setErroFotos(null)
    setEnviandoFotos(true)
    try {
      const atualizado = await adicionarFotosAnuncio(anuncioId, novasFotos)
      if (!montado.current) return
      setFotos(atualizado.fotos)
      setNovasFotos([])
      // achado no code review: limpar só o estado novasFotos não muda o valor do <input type="file">
      // no DOM — ele continuava mostrando os arquivos já enviados, e reselecionar o(s) mesmo(s)
      // arquivo(s) não dispararia onChange (o browser só notifica quando o value muda de fato)
      if (inputFotosRef.current) inputFotosRef.current.value = ''
    } catch (erro) {
      if (!montado.current) return
      const mensagem = erro instanceof AnuncioApiError ? erro.message : 'Não foi possível adicionar as fotos. Tente novamente.'
      setErroFotos(mensagem)
    } finally {
      if (montado.current) setEnviandoFotos(false)
    }
  }

  // confirmação inline por-foto — nunca window.confirm() nativo (UX-DR12), mesmo espírito de
  // confirmandoExclusao (Story 2.4), só que por-foto em vez de tela cheia
  async function aoConfirmarRemoverFoto(fotoId: string) {
    setErroFotos(null)
    setRemovendoFoto(true)
    try {
      const atualizado = await removerFotoAnuncio(anuncioId, fotoId)
      if (!montado.current) return
      setFotos(atualizado.fotos)
      setFotoParaRemover(null)
    } catch (erro) {
      if (!montado.current) return
      const mensagem = erro instanceof AnuncioApiError ? erro.message : 'Não foi possível remover a foto. Tente novamente.'
      setErroFotos(mensagem)
    } finally {
      if (montado.current) setRemovendoFoto(false)
    }
  }

  // um Anúncio ativo nunca pode ficar com a Ficha incompleta (mesma regra de Anuncio.AtualizarFicha/
  // Publicar no backend, AD-1: servidor é a fonte de verdade, isto só evita uma viagem desnecessária).
  // Rascunho aceita qualquer combinação de campos, inclusive em branco — nenhuma validação aqui.
  function validarSeAtivo(): boolean {
    if (status !== 'Ativo') return true
    const erros: Partial<Record<CampoErro, string>> = {}
    if (!form.marca.trim()) erros.marca = 'Informe a marca.'
    if (!form.modelo.trim()) erros.modelo = 'Informe o modelo.'
    if (!form.ano.trim()) erros.ano = 'Informe o ano.'
    if (!form.versao.trim()) erros.versao = 'Informe a versão.'
    if (!form.preco.trim()) erros.preco = 'Informe o preço.'
    if (!form.descricao.trim()) erros.descricao = 'Informe a descrição.'
    if (!form.estado.trim()) erros.estado = 'Informe o estado.'
    if (!form.cidade.trim()) erros.cidade = 'Informe a cidade.'
    setErrosCampo(erros)
    return Object.keys(erros).length === 0
  }

  async function aoSubmeter(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErroEnvio(null)

    if (!validarSeAtivo()) return

    setEnviando(true)
    try {
      // Ano/Preco vão como number | null no corpo JSON — uma string vazia ("") não é um número JSON
      // válido e quebraria o binding de int?/decimal? no backend antes mesmo da validação de domínio
      await editarAnuncio(anuncioId, {
        ...form,
        ano: form.ano.trim() === '' ? null : Number(form.ano),
        preco: form.preco.trim() === '' ? null : Number(form.preco),
      })
      if (!montado.current) return
      setSalvo(true)
    } catch (erro) {
      if (!montado.current) return
      const mensagem =
        erro instanceof AnuncioApiError ? erro.message : 'Não foi possível salvar as alterações. Tente novamente.'
      setErroEnvio(mensagem)
    } finally {
      if (montado.current) setEnviando(false)
    }
  }

  const ocupado = enviando || transicionando || excluindo

  // AC #1: excluir nunca chama a API direto no clique — só troca pra uma tela de confirmação
  // inline (nunca window.confirm() nativo, tom calmo/editorial, UX-DR12); só "Sim, excluir" chama
  // excluirAnuncio de fato. Disponível em qualquer status — nenhuma AC restringe por status
  async function aoConfirmarExclusao() {
    setErroEnvio(null)
    setExcluindo(true)
    try {
      await excluirAnuncio(anuncioId)
      if (!montado.current) return
      setExcluido(true)
    } catch (erro) {
      if (!montado.current) return
      // mantém confirmandoExclusao=true — o erro aparece dentro do próprio diálogo de confirmação
      // (não descarta a intenção de excluir nem manda o usuário de volta pro formulário principal)
      const mensagem = erro instanceof AnuncioApiError ? erro.message : 'Não foi possível excluir o Anúncio. Tente novamente.'
      setErroEnvio(mensagem)
    } finally {
      if (montado.current) setExcluindo(false)
    }
  }

  const ROTULO_POR_STATUS: Partial<Record<StatusAnuncio, 'pausado' | 'reativado' | 'vendido'>> = {
    Pausado: 'pausado',
    Ativo: 'reativado',
    Vendido: 'vendido',
  }

  // Pausar/Reativar/Marcar como vendido não são a ação primária da tela (Salvar continua sendo,
  // UX-DR8) — mesmo estilo secundário já usado pro alternador Login/Cadastro e pro Salvar rascunho.
  // `ocupado` (Salvar OU uma transição) desabilita todos os botões de ação juntos — evita disparar
  // um Salvar e uma transição de status ao mesmo tempo sobre o mesmo Anúncio
  async function executarTransicao(acao: () => Promise<AnuncioResponse>) {
    setErroEnvio(null)
    setTransicionando(true)
    try {
      const atualizado = await acao()
      if (!montado.current) return
      setStatus(atualizado.status)
      // rótulo da confirmação vem do status retornado pelo servidor, não da intenção do
      // chamador — evita mostrar uma confirmação errada se a resposta divergir do esperado
      setAcaoStatusConcluida(ROTULO_POR_STATUS[atualizado.status] ?? null)
    } catch (erro) {
      if (!montado.current) return
      const mensagem = erro instanceof AnuncioApiError ? erro.message : 'Não foi possível concluir a ação. Tente novamente.'
      setErroEnvio(mensagem)
    } finally {
      if (montado.current) setTransicionando(false)
    }
  }

  if (carregando) {
    return (
      <div className="auth-form" role="status">
        <p className="auth-form__corpo">Carregando…</p>
      </div>
    )
  }

  if (erroCarregar) {
    return (
      <div className="auth-form" role="alert">
        <p className="auth-form__erro-envio">{erroCarregar}</p>
        <button type="button" className="auth-form__alternar" onClick={() => setTentativa((n) => n + 1)}>
          Tentar novamente
        </button>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar aos {tituloVoltar}
          </button>
        )}
      </div>
    )
  }

  if (salvo) {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Alterações salvas</h1>
        <p className="auth-form__corpo">O Anúncio foi atualizado.</p>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar aos {tituloVoltar}
          </button>
        )}
      </div>
    )
  }

  if (acaoStatusConcluida === 'pausado') {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Anúncio pausado</h1>
        <p className="auth-form__corpo">Ele não aparece mais na busca até você reativar.</p>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar aos {tituloVoltar}
          </button>
        )}
      </div>
    )
  }

  if (acaoStatusConcluida === 'reativado') {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Anúncio reativado</h1>
        <p className="auth-form__corpo">Ele está visível na busca novamente.</p>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar aos {tituloVoltar}
          </button>
        )}
      </div>
    )
  }

  if (acaoStatusConcluida === 'vendido') {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Anúncio marcado como vendido</h1>
        <p className="auth-form__corpo">Ele não aparece mais na busca.</p>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar aos {tituloVoltar}
          </button>
        )}
      </div>
    )
  }

  if (excluido) {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Anúncio excluído</h1>
        <p className="auth-form__corpo">Ele foi removido permanentemente.</p>
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar aos {tituloVoltar}
          </button>
        )}
        <button type="button" className="cta-button" onClick={() => onExcluido?.()}>
          Criar novo Anúncio
        </button>
      </div>
    )
  }

  if (confirmandoExclusao) {
    return (
      <div className="auth-form" role="alertdialog">
        <h1 className="auth-form__titulo">Excluir este Anúncio?</h1>
        <p className="auth-form__corpo">Essa ação não pode ser desfeita.</p>
        {erroEnvio && (
          <p className="auth-form__erro-envio" role="alert">
            {erroEnvio}
          </p>
        )}
        <button type="button" className="auth-form__alternar" disabled={excluindo} onClick={() => setConfirmandoExclusao(false)}>
          Cancelar
        </button>
        <button type="button" className="cta-button" disabled={excluindo} onClick={() => void aoConfirmarExclusao()}>
          {excluindo ? 'Excluindo…' : 'Sim, excluir'}
        </button>
      </div>
    )
  }

  return (
    <form className="auth-form" onSubmit={aoSubmeter} noValidate>
      {onVoltar && (
        <button type="button" className="auth-form__alternar" onClick={onVoltar}>
          ← Voltar aos {tituloVoltar}
        </button>
      )}
      <h1 className="auth-form__titulo">Editar Anúncio</h1>

      <ComboboxCascata
        id={marcaId}
        label="Marca"
        valor={form.marca}
        opcoes={marcas}
        erro={erroMarcas ?? undefined}
        onTentarNovamente={() => setTentativaMarcas((n) => n + 1)}
        onSelecionar={aoSelecionarMarca}
        mensagemSemResultado="Nenhuma marca encontrada."
        invalido={Boolean(errosCampo.marca)}
        erroId={`${marcaId}-erro`}
      />
      {errosCampo.marca && (
        <span id={`${marcaId}-erro`} className="auth-form__erro-campo" role="alert">
          {errosCampo.marca}
        </span>
      )}

      <ComboboxCascata
        id={modeloId}
        label="Modelo"
        valor={form.modelo}
        opcoes={modelos}
        erro={erroModelos ?? undefined}
        onTentarNovamente={() => setTentativaModelos((n) => n + 1)}
        onSelecionar={aoSelecionarModelo}
        desabilitado={!marcaCodigo}
        // achado no code review: "Escolha a marca primeiro" é factualmente incorreto quando já
        // existe uma Marca salva (Anúncio antigo) sem correspondência na Fipe — mensagem distinta
        // pra esse caso, não a mesma usada quando Marca está genuinamente em branco
        placeholderDesabilitado={form.marca ? 'Marca não reconhecida pela Fipe — escolha novamente' : 'Escolha a marca primeiro'}
        mensagemSemResultado="Nenhum modelo encontrado."
        invalido={Boolean(errosCampo.modelo)}
        erroId={`${modeloId}-erro`}
      />
      {errosCampo.modelo && (
        <span id={`${modeloId}-erro`} className="auth-form__erro-campo" role="alert">
          {errosCampo.modelo}
        </span>
      )}

      {CAMPOS.map(([campo, rotulo, tipo]) => {
        const id = idsPorCampo[campo]
        const erro = errosCampo[campo as CampoErro]
        return (
          <div className="auth-form__campo" key={campo}>
            <label htmlFor={id}>{rotulo}</label>
            <input
              id={id}
              type={tipo}
              value={form[campo]}
              onChange={(evento) => atualizarCampo(campo, evento.target.value)}
              aria-invalid={Boolean(erro)}
              aria-describedby={erro ? `${id}-erro` : undefined}
              // achado em teste manual do usuário: sem isso o campo aceitava qualquer Ano (ex.
              // 1897) e salvava — a garantia real fica no backend (Domain), isto é só UX
              {...(campo === 'ano' ? { min: ANO_MINIMO, max: new Date().getFullYear() + 1 } : {})}
            />
            {erro && (
              <span id={`${id}-erro`} className="auth-form__erro-campo" role="alert">
                {erro}
              </span>
            )}
          </div>
        )
      })}

      {/* achado em teste manual do usuário: Descrição precisa comportar texto de verdade
          (parágrafos), não uma linha só — <textarea> no lugar do <input type="text"> genérico */}
      <div className="auth-form__campo">
        <label htmlFor={descricaoId}>Descrição</label>
        <textarea
          id={descricaoId}
          rows={4}
          value={form.descricao}
          onChange={(evento) => atualizarCampo('descricao', evento.target.value)}
          aria-invalid={Boolean(errosCampo.descricao)}
          aria-describedby={errosCampo.descricao ? `${descricaoId}-erro` : undefined}
        />
        {errosCampo.descricao && (
          <span id={`${descricaoId}-erro`} className="auth-form__erro-campo" role="alert">
            {errosCampo.descricao}
          </span>
        )}
      </div>

      <ComboboxCascata
        id={estadoId}
        label="Estado"
        valor={form.estado}
        opcoes={estados}
        erro={erroEstados ?? undefined}
        onTentarNovamente={() => setTentativaEstados((n) => n + 1)}
        onSelecionar={aoSelecionarEstado}
        mensagemSemResultado="Nenhum estado encontrado."
        invalido={Boolean(errosCampo.estado)}
        erroId={`${estadoId}-erro`}
      />
      {errosCampo.estado && (
        <span id={`${estadoId}-erro`} className="auth-form__erro-campo" role="alert">
          {errosCampo.estado}
        </span>
      )}

      <ComboboxCascata
        id={cidadeId}
        label="Cidade"
        valor={form.cidade}
        opcoes={municipios}
        erro={erroMunicipios ?? undefined}
        onTentarNovamente={() => setTentativaMunicipios((n) => n + 1)}
        onSelecionar={aoSelecionarCidade}
        desabilitado={!estadoSigla}
        placeholderDesabilitado={form.estado ? 'Estado não reconhecido — escolha novamente' : 'Escolha o estado primeiro'}
        mensagemSemResultado="Nenhuma cidade encontrada."
        invalido={Boolean(errosCampo.cidade)}
        erroId={`${cidadeId}-erro`}
      />
      {errosCampo.cidade && (
        <span id={`${cidadeId}-erro`} className="auth-form__erro-campo" role="alert">
          {errosCampo.cidade}
        </span>
      )}

      <div className="auth-form__campo">
        {/* achado no code review: <label> sem htmlFor não está associado a nenhum controle — não
            ajuda leitor de tela, só parece um rótulo visualmente. Mesmo elemento já usado pro
            rótulo do grupo de rádio Pessoa Física/Lojista (AuthForm.css) */}
        <p className="auth-form__micro-label">Fotos</p>
        {fotos.length > 0 && (
          <ul className="auth-form__fotos-grid">
            {fotos.map((foto, indice) => (
              <li key={foto.id} className="auth-form__foto-item">
                <img className="auth-form__foto-thumb" src={foto.url} alt="" />
                {fotoParaRemover === foto.id ? (
                  <div className="auth-form__foto-confirmar">
                    <span>Remover esta foto?</span>
                    <div className="auth-form__foto-confirmar-acoes">
                      <button
                        type="button"
                        className="auth-form__alternar"
                        disabled={removendoFoto || enviandoFotos}
                        onClick={() => setFotoParaRemover(null)}
                      >
                        Cancelar
                      </button>
                      <button
                        type="button"
                        className="auth-form__alternar"
                        disabled={removendoFoto || enviandoFotos}
                        onClick={() => void aoConfirmarRemoverFoto(foto.id)}
                      >
                        {removendoFoto ? 'Removendo…' : 'Sim, remover'}
                      </button>
                    </div>
                  </div>
                ) : (
                  // achado no code review: "Remover" sozinho é indistinguível entre as fotos pra
                  // quem navega por leitor de tela (todas anunciam "Remover, botão") — aria-label
                  // identifica qual foto cada botão afeta, sem mudar o texto visível
                  <button
                    type="button"
                    className="auth-form__alternar"
                    aria-label={`Remover foto ${indice + 1}`}
                    disabled={removendoFoto || enviandoFotos}
                    onClick={() => setFotoParaRemover(foto.id)}
                  >
                    Remover
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}

        <label htmlFor={fotosId}>Adicionar fotos</label>
        <input
          ref={inputFotosRef}
          id={fotosId}
          type="file"
          multiple
          accept="image/jpeg,image/png,image/webp"
          disabled={enviandoFotos || removendoFoto}
          onChange={(evento) => setNovasFotos(Array.from(evento.target.files ?? []))}
        />
        <button
          type="button"
          className="auth-form__alternar"
          disabled={novasFotos.length === 0 || enviandoFotos || removendoFoto}
          onClick={() => void aoAdicionarFotos()}
        >
          {enviandoFotos ? 'Enviando…' : 'Adicionar fotos'}
        </button>
        {erroFotos && (
          <p className="auth-form__erro-envio" role="alert">
            {erroFotos}
          </p>
        )}
      </div>

      {erroEnvio && (
        <p className="auth-form__erro-envio" role="alert">
          {erroEnvio}
        </p>
      )}

      <button type="submit" className="cta-button" disabled={ocupado}>
        {enviando ? 'Salvando…' : 'Salvar'}
      </button>

      {status === 'Ativo' && (
        <button
          type="button"
          className="auth-form__alternar"
          disabled={ocupado}
          onClick={() => void executarTransicao(() => pausarAnuncio(anuncioId))}
        >
          Pausar
        </button>
      )}

      {status === 'Pausado' && (
        <button
          type="button"
          className="auth-form__alternar"
          disabled={ocupado}
          onClick={() => void executarTransicao(() => reativarAnuncio(anuncioId))}
        >
          Reativar
        </button>
      )}

      {(status === 'Ativo' || status === 'Pausado') && (
        <button
          type="button"
          className="auth-form__alternar"
          disabled={ocupado}
          onClick={() => void executarTransicao(() => marcarAnuncioVendido(anuncioId))}
        >
          Marcar como vendido
        </button>
      )}

      <button type="button" className="auth-form__alternar" disabled={ocupado} onClick={() => setConfirmandoExclusao(true)}>
        Excluir Anúncio
      </button>
    </form>
  )
}
