import { useEffect, useId, useState, type FormEvent } from 'react'
import '../autenticacao/AuthForm.css'
import { AnuncioApiError, criarAnuncio } from './api'
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

const estadoInicial: FormState = {
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
// ComboboxCascata, renderizado explicitamente antes deste map. Os outros 4 campos continuam texto livre
const CAMPOS: ReadonlyArray<readonly [Exclude<keyof FormState, 'marca' | 'modelo' | 'estado' | 'cidade'>, string, 'text' | 'number']> = [
  ['ano', 'Ano', 'number'],
  ['versao', 'Versão', 'text'],
  ['preco', 'Preço', 'number'],
  ['descricao', 'Descrição', 'text'],
]

interface CriarAnuncioFormProps {
  // Story 4.4, achado no code review — o rótulo do link "Voltar aos..." tinha "Meus Anúncios"
  // hardcoded, divergindo do "Painel do Lojista" pra quem chega vindo de lá
  tituloVoltar?: RotuloPainel
  onEditar?: (anuncioId: string) => void
  onVoltar?: () => void
}

export function CriarAnuncioForm({ tituloVoltar = 'Meus Anúncios', onEditar, onVoltar }: CriarAnuncioFormProps = {}) {
  const [form, setForm] = useState<FormState>(estadoInicial)
  const [fotos, setFotos] = useState<File[]>([])
  const [errosCampo, setErrosCampo] = useState<Partial<Record<CampoErro, string>>>({})
  const [erroEnvio, setErroEnvio] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [concluido, setConcluido] = useState<'rascunho' | 'publicado' | null>(null)
  const [anuncioId, setAnuncioId] = useState<string | null>(null)

  // Story 2.6 — Marca/Modelo via Fipe. marcaCodigo (não persistido, só usado pra buscar os
  // modelos em cascata) fica separado de form.marca (o nome, que é o que de fato é salvo)
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
        // achado no code review: sem setar marcas aqui, opcoes fica undefined pra sempre após
        // uma falha, e o ComboboxCascata mostra "Carregando…" e a mensagem de erro ao mesmo tempo
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

  // Story 2.7 — Estado/Cidade via dataset estático do IBGE (AD-12: sem backend novo, dataset
  // embarcado). Achado no code review: "estático" não é "sem chamada de rede" — fetch() de um
  // asset do próprio domínio ainda pode falhar, e Estado/Cidade são obrigatórios pra publicar,
  // então ganham o mesmo tratamento de erro/retry de Marca/Modelo, não a ausência dele
  const [estados, setEstados] = useState<OpcaoCombobox[] | undefined>(undefined)
  const [erroEstados, setErroEstados] = useState<string | null>(null)
  const [tentativaEstados, setTentativaEstados] = useState(0)
  const [estadoSigla, setEstadoSigla] = useState<string | null>(null)
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

  function validarParaPublicar(): boolean {
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

  async function enviar(publicar: boolean) {
    setErroEnvio(null)

    // validação client-side só bloqueia o Publicar — Salvar rascunho sempre envia, servidor é quem decide (AD-1)
    if (publicar && !validarParaPublicar()) return

    setEnviando(true)
    try {
      const anuncio = await criarAnuncio({ ...form, publicar, fotos })
      setAnuncioId(anuncio.id)
      setConcluido(anuncio.status === 'Ativo' ? 'publicado' : 'rascunho')
    } catch (erro) {
      const mensagem =
        erro instanceof AnuncioApiError ? erro.message : 'Não foi possível salvar o Anúncio. Tente novamente.'
      setErroEnvio(mensagem)
    } finally {
      setEnviando(false)
    }
  }

  function aoSubmeter(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    void enviar(true)
  }

  if (concluido === 'publicado') {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Anúncio publicado</h1>
        <p className="auth-form__corpo">Seu veículo já está visível para Compradores.</p>
        {onEditar && anuncioId && (
          <button type="button" className="auth-form__alternar" onClick={() => onEditar(anuncioId)}>
            Editar este Anúncio
          </button>
        )}
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar aos {tituloVoltar}
          </button>
        )}
      </div>
    )
  }

  if (concluido === 'rascunho') {
    return (
      <div className="auth-form" role="status">
        <h1 className="auth-form__titulo">Rascunho salvo</h1>
        <p className="auth-form__corpo">Você pode continuar editando e publicar quando quiser.</p>
        {onEditar && anuncioId && (
          <button type="button" className="auth-form__alternar" onClick={() => onEditar(anuncioId)}>
            Editar este Anúncio
          </button>
        )}
        {onVoltar && (
          <button type="button" className="auth-form__alternar" onClick={onVoltar}>
            Voltar aos {tituloVoltar}
          </button>
        )}
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
      <h1 className="auth-form__titulo">Anunciar veículo</h1>

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
        placeholderDesabilitado="Escolha a marca primeiro"
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
            />
            {erro && (
              <span id={`${id}-erro`} className="auth-form__erro-campo" role="alert">
                {erro}
              </span>
            )}
          </div>
        )
      })}

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
        placeholderDesabilitado="Escolha o estado primeiro"
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
        <label htmlFor={fotosId}>Fotos (opcional)</label>
        <input
          id={fotosId}
          type="file"
          multiple
          accept="image/jpeg,image/png,image/webp"
          onChange={(evento) => setFotos(Array.from(evento.target.files ?? []))}
        />
      </div>

      {erroEnvio && (
        <p className="auth-form__erro-envio" role="alert">
          {erroEnvio}
        </p>
      )}

      {/* Publicar é a única ação primária da tela (UX-DR8) — Salvar rascunho fica com o estilo
          secundário já usado pelo alternador Login/Cadastro, não outro cta-button */}
      <button type="button" className="auth-form__alternar" disabled={enviando} onClick={() => void enviar(false)}>
        {enviando ? 'Salvando…' : 'Salvar rascunho'}
      </button>
      <button type="submit" className="cta-button" disabled={enviando}>
        {enviando ? 'Publicando…' : 'Publicar'}
      </button>
    </form>
  )
}
