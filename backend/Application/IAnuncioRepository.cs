using Carango.Domain;

namespace Carango.Application;

public interface IAnuncioRepository
{
    Task AdicionarAsync(Anuncio anuncio);

    Task<int> ContarAtivosPorVendedorAsync(Guid vendedorId);

    Task<Anuncio?> ObterPorIdAsync(Guid id);

    Task AtualizarAsync(Anuncio anuncio);

    Task ExcluirAsync(Anuncio anuncio);

    Task<IReadOnlyList<Anuncio>> ListarPorVendedorAsync(Guid vendedorId);

    // Status = Ativo é imposto sempre dentro da implementação, não é um campo de FiltroBusca —
    // não é opcional, é a regra da busca pública (AC #1 da Story 3.1), caminho novo e distinto
    // de ListarPorVendedorAsync (owner-only, todos os status, Story 2.5)
    Task<IReadOnlyList<Anuncio>> BuscarAsync(FiltroBusca filtro);

    // detalhe público (Story 3.5) — mesma regra de BuscarAsync (só Ativo), distinta de
    // ObterPorIdAsync (que retorna qualquer status, usado pelo caminho owner-only)
    Task<Anuncio?> ObterAtivoPorIdAsync(Guid id);

    // método dedicado (Story 4.5), não reaproveita ObterAtivoPorIdAsync+AtualizarAsync —
    // ObterAtivoPorIdAsync usa .AsNoTracking(), então AtualizarAsync (que depende só de
    // tracking ambiente, sem Update()/Attach() explícito) seria um no-op silencioso sobre
    // essa entidade. Faz sua própria busca rastreada, incrementa e salva
    Task IncrementarVisualizacaoAsync(Guid id);
}
