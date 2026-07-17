import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import './ComboboxCascata.css'

export interface OpcaoCombobox {
  codigo: string
  nome: string
}

interface ComboboxCascataProps {
  id?: string
  label: string
  valor: string
  onSelecionar: (opcao: OpcaoCombobox) => void
  // undefined = carregando; lista vazia é um estado válido (ex.: marca sem nenhum modelo)
  opcoes: OpcaoCombobox[] | undefined
  erro?: string
  onTentarNovamente?: () => void
  desabilitado?: boolean
  placeholderDesabilitado?: string
  // achado no code review: "Nenhuma opção encontrada." genérico não bate com o texto exigido
  // por DESIGN.md ("Nenhuma marca encontrada"/"Nenhum modelo encontrado") — cada uso informa o
  // texto certo em vez do componente tentar adivinhar gênero a partir de `label`
  mensagemSemResultado?: string
  // achado no code review: aria-invalid/aria-describedby se perderam quando Marca/Modelo saíram
  // do CAMPOS.map genérico que já aplicava isso a todo campo — repassados explicitamente aqui
  invalido?: boolean
  erroId?: string
}

// combobox-cascata (DESIGN.md/EXPERIENCE.md, Sprint Change Proposal 2026-07-13) — estado fechado
// idêntico a um <input type="text"> comum; abre como overlay de tela cheia no mobile (<768px,
// via CSS, mesmo padrão do Detalhe do Anúncio) ou dropdown inline no desktop/tablet (≥768px).
// Primeiro componente de UI verdadeiramente compartilhado do projeto — reaproveitado pelas
// Stories 2.7/3.7/3.8 (ver Dev Notes da Story 2.6)
export function ComboboxCascata({
  id,
  label,
  valor,
  onSelecionar,
  opcoes,
  erro,
  onTentarNovamente,
  desabilitado = false,
  placeholderDesabilitado,
  mensagemSemResultado = 'Nenhuma opção encontrada.',
  invalido = false,
  erroId,
}: ComboboxCascataProps) {
  const [aberto, setAberto] = useState(false)
  const [termo, setTermo] = useState('')
  const [indiceAtivo, setIndiceAtivo] = useState(0)
  const campoFechadoRef = useRef<HTMLInputElement>(null)
  const buscaRef = useRef<HTMLInputElement>(null)
  const raizRef = useRef<HTMLDivElement>(null)

  const idGerado = useId()
  const inputId = id ?? idGerado
  const listboxId = `${inputId}-listbox`

  const opcoesFiltradas = (opcoes ?? []).filter((opcao) => opcao.nome.toLowerCase().includes(termo.toLowerCase()))
  const mostrandoLista = opcoes !== undefined && !erro && opcoesFiltradas.length > 0

  // achado no code review: sem isso, o painel (overlay de tela cheia no mobile) só fechava via
  // Escape/seleção/botão "×" (escondido no desktop) — clicar/tabular pra fora deixava o overlay
  // aberto indefinidamente, bloqueando o resto do formulário atrás dele
  useEffect(() => {
    if (!aberto) return
    function aoClicarFora(evento: MouseEvent) {
      if (raizRef.current && !raizRef.current.contains(evento.target as Node)) fechar()
    }
    document.addEventListener('mousedown', aoClicarFora)
    return () => document.removeEventListener('mousedown', aoClicarFora)
  }, [aberto])

  function abrir() {
    if (desabilitado) return
    setAberto(true)
    setTermo('')
    setIndiceAtivo(0)
  }

  function fechar() {
    setAberto(false)
    setTermo('')
  }

  function selecionar(opcao: OpcaoCombobox) {
    onSelecionar(opcao)
    fechar()
    campoFechadoRef.current?.focus()
  }

  function aoTeclar(evento: KeyboardEvent<HTMLInputElement>) {
    if (desabilitado) return

    if (evento.key === 'ArrowDown') {
      evento.preventDefault()
      if (!aberto) {
        abrir()
        return
      }
      setIndiceAtivo((indice) => Math.min(indice + 1, opcoesFiltradas.length - 1))
    } else if (evento.key === 'ArrowUp') {
      evento.preventDefault()
      setIndiceAtivo((indice) => Math.max(indice - 1, 0))
    } else if (evento.key === 'Enter') {
      evento.preventDefault()
      if (aberto && opcoesFiltradas[indiceAtivo]) selecionar(opcoesFiltradas[indiceAtivo])
    } else if (evento.key === 'Escape') {
      evento.preventDefault()
      fechar()
      campoFechadoRef.current?.focus()
    }
  }

  const idOpcaoAtiva = mostrandoLista && opcoesFiltradas[indiceAtivo] ? `${listboxId}-opcao-${indiceAtivo}` : undefined

  return (
    <div className="auth-form__campo combobox-cascata" ref={raizRef}>
      <label htmlFor={inputId}>{label}</label>
      {/* estado fechado — idêntico a um <input type="text"> comum (DESIGN.md); vira só exibição
          assim que o painel abre, quem recebe a digitação de verdade é o campo de busca dentro
          do próprio painel (ver abaixo) */}
      <input
        ref={campoFechadoRef}
        id={inputId}
        readOnly
        disabled={desabilitado}
        placeholder={desabilitado ? placeholderDesabilitado : undefined}
        value={valor}
        aria-invalid={invalido}
        aria-describedby={invalido && erroId ? erroId : undefined}
        // achado em teste manual do usuário: abrir no onFocus reabria o painel sozinho, porque
        // selecionar()/Escape focam este mesmo campo (fechado) logo depois de fechar() — o foco
        // programático disparava onFocus de novo, cancelando o fechamento. .focus() programático
        // não dispara onClick, então isso resolve sem quebrar teclado (ArrowDown já abre via aoTeclar)
        onClick={abrir}
        onKeyDown={aoTeclar}
      />

      {aberto && (
        <div className="combobox-cascata__painel">
          <button type="button" className="combobox-cascata__fechar" aria-label="Fechar" onClick={fechar}>
            ×
          </button>

          {/* campo de busca de verdade — DESIGN.md: "overlay mostra: campo de busca no topo
              (foco automático, teclado abre)". Achado no code review: antes o campo interativo
              ficava FORA do painel, e o overlay de tela cheia do mobile cobria ele visualmente,
              tornando o picker inutilizável nesse viewport */}
          <input
            ref={buscaRef}
            className="combobox-cascata__busca"
            role="combobox"
            aria-expanded={aberto}
            aria-controls={mostrandoLista ? listboxId : undefined}
            aria-activedescendant={idOpcaoAtiva}
            aria-autocomplete="list"
            aria-label={label}
            autoComplete="off"
            autoFocus
            value={termo}
            onChange={(evento) => {
              setTermo(evento.target.value)
              setIndiceAtivo(0)
            }}
            onKeyDown={aoTeclar}
          />

          {opcoes === undefined && !erro && <p className="combobox-cascata__mensagem">Carregando…</p>}

          {erro && (
            <div className="combobox-cascata__mensagem" role="alert">
              <p>{erro}</p>
              {onTentarNovamente && (
                <button type="button" className="auth-form__alternar" onClick={onTentarNovamente}>
                  Tentar novamente
                </button>
              )}
            </div>
          )}

          {opcoes !== undefined && !erro && opcoesFiltradas.length === 0 && (
            <p className="combobox-cascata__mensagem">{mensagemSemResultado}</p>
          )}

          {mostrandoLista && (
            <ul className="combobox-cascata__lista" id={listboxId} role="listbox">
              {opcoesFiltradas.map((opcao, indice) => (
                <li
                  key={opcao.codigo}
                  id={`${listboxId}-opcao-${indice}`}
                  role="option"
                  aria-selected={indice === indiceAtivo}
                  className={indice === indiceAtivo ? 'combobox-cascata__opcao combobox-cascata__opcao--ativa' : 'combobox-cascata__opcao'}
                  onMouseEnter={() => setIndiceAtivo(indice)}
                  onClick={() => selecionar(opcao)}
                >
                  {opcao.nome}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
