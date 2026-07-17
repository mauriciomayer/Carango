using Carango.Domain;

namespace Carango.Application;

// Serviço novo, não uma extensão de GerenciarAnuncioService: o ator aqui é o Comprador,
// não autenticado, sem posse sobre nada — diferente de todo método de GerenciarAnuncioService,
// que pressupõe um Vendedor autenticado dono do próprio Anúncio (AD-9 não se aplica aqui)
public class BuscarAnunciosService
{
    private readonly IAnuncioRepository _repositorio;

    public BuscarAnunciosService(IAnuncioRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public Task<IReadOnlyList<Anuncio>> BuscarAsync(FiltroBusca filtro) => _repositorio.BuscarAsync(filtro);

    public async Task<Anuncio> ObterDetalhePublicoAsync(Guid id)
    {
        var anuncio = await _repositorio.ObterAtivoPorIdAsync(id);
        if (anuncio is null)
            throw new AnuncioNaoEncontradoException();

        // melhor esforço (Story 4.5) — uma falha ao registrar a métrica não pode quebrar o
        // Detalhe pro Comprador, que é o próprio serviço este incremento piggybacka em cima.
        // Busca própria e rastreada dentro de IncrementarVisualizacaoAsync, não reaproveita o
        // `anuncio` acima (veio de ObterAtivoPorIdAsync, .AsNoTracking() — ver IAnuncioRepository)
        try
        {
            await _repositorio.IncrementarVisualizacaoAsync(id);
        }
        catch
        {
            // registro da métrica é secundário ao próprio Detalhe funcionar — ver Dev Notes da Story 4.5
        }

        return anuncio;
    }
}
