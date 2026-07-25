import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ComboboxCascata, type OpcaoCombobox } from './ComboboxCascata'

// Story 5.1 — primeiro componente do frontend a ganhar teste automatizado. Escolhido por ser o
// mais reutilizado do projeto (Marca/Modelo e Estado/Cidade, em 3 telas diferentes) e por ser
// onde esta sessão encontrou uma regressão real em produção (ver caso "regressão" abaixo).
// Fixture local — o componente é puro em termos de dados, nunca busca nada sozinho, então nenhum
// mock de rede (Fipe/IBGE/backend) é necessário aqui.
const OPCOES: OpcaoCombobox[] = [
  { codigo: '1', nome: 'Honda' },
  { codigo: '2', nome: 'Toyota' },
  { codigo: '3', nome: 'Fiat' },
]

function renderCombobox(onSelecionar = vi.fn()) {
  render(<ComboboxCascata label="Marca" valor="" opcoes={OPCOES} onSelecionar={onSelecionar} />)
  return { onSelecionar }
}

describe('ComboboxCascata', () => {
  it('começa fechado, sem o painel de opções no DOM', () => {
    renderCombobox()

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('abre o painel ao clicar no campo fechado', async () => {
    const user = userEvent.setup()
    renderCombobox()

    await user.click(screen.getByLabelText('Marca'))

    expect(screen.getByRole('listbox')).toBeInTheDocument()
    expect(screen.getByText('Honda')).toBeInTheDocument()
    expect(screen.getByText('Toyota')).toBeInTheDocument()
    expect(screen.getByText('Fiat')).toBeInTheDocument()
  })

  it('ArrowDown abre o painel quando o campo fechado está focado', async () => {
    const user = userEvent.setup()
    renderCombobox()
    screen.getByLabelText('Marca').focus()

    await user.keyboard('{ArrowDown}')

    expect(screen.getByRole('listbox')).toBeInTheDocument()
  })

  it('selecionar uma opção chama onSelecionar com a opção certa', async () => {
    const user = userEvent.setup()
    const { onSelecionar } = renderCombobox()

    await user.click(screen.getByLabelText('Marca'))
    await user.click(screen.getByText('Toyota'))

    expect(onSelecionar).toHaveBeenCalledWith({ codigo: '2', nome: 'Toyota' })
  })

  // regressão achada em teste manual do usuário nesta sessão: selecionar() chamava .focus() no
  // campo fechado logo depois de fechar() — e esse campo tinha onFocus={abrir}, então o foco
  // programático reabria o painel na mesma sequência de eventos. Em jsdom, .focus() dispara um
  // evento de foco de verdade, então este teste falharia com o onFocus antigo e passa com o
  // onClick atual — é o teste que teria pego o bug antes de chegar em produção.
  it('selecionar uma opção fecha o painel e NÃO reabre sozinho', async () => {
    const user = userEvent.setup()
    renderCombobox()

    await user.click(screen.getByLabelText('Marca'))
    await user.click(screen.getByText('Toyota'))

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('Escape fecha o painel e devolve o foco ao campo fechado', async () => {
    const user = userEvent.setup()
    renderCombobox()

    await user.click(screen.getByLabelText('Marca'))
    await user.keyboard('{Escape}')

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Marca')).toHaveFocus()
  })

  it('navega as opções com as setas e seleciona com Enter', async () => {
    const user = userEvent.setup()
    const { onSelecionar } = renderCombobox()

    await user.click(screen.getByLabelText('Marca'))
    await user.keyboard('{ArrowDown}') // índice 0 (Honda) -> 1 (Toyota)
    await user.keyboard('{Enter}')

    expect(onSelecionar).toHaveBeenCalledWith({ codigo: '2', nome: 'Toyota' })
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('clicar fora do componente fecha o painel', async () => {
    const user = userEvent.setup()
    render(
      <div>
        <ComboboxCascata label="Marca" valor="" opcoes={OPCOES} onSelecionar={vi.fn()} />
        <button type="button">Fora</button>
      </div>,
    )

    await user.click(screen.getByLabelText('Marca'))
    expect(screen.getByRole('listbox')).toBeInTheDocument()

    await user.click(screen.getByText('Fora'))

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('digitar no campo de busca filtra as opções mostradas', async () => {
    const user = userEvent.setup()
    renderCombobox()

    await user.click(screen.getByLabelText('Marca'))
    await user.type(screen.getByRole('combobox', { name: 'Marca' }), 'Toy')

    expect(screen.getByText('Toyota')).toBeInTheDocument()
    expect(screen.queryByText('Honda')).not.toBeInTheDocument()
  })
})
