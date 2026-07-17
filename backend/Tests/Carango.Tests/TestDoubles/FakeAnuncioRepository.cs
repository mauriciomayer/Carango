using Carango.Application;
using Carango.Domain;

namespace Carango.Tests.TestDoubles;

public class FakeAnuncioRepository : IAnuncioRepository
{
    // mesmo valor de AnuncioRepository.TamanhoPagina (Story 3.4) — duplicação já aceita entre
    // real/fake pros outros aspectos da query desde a Story 3.1 (ver deferred-work.md)
    private const int TamanhoPagina = 20;

    // mesmo valor de AnuncioRepository.MaxTokensTermoLivre (Story 3.3) — achado no code review
    // da Story 3.4: estava só como `.Take(10)` sem constante nomeada, inconsistente com
    // TamanhoPagina logo acima (que já é documentada e referenciada)
    private const int MaxTokensTermoLivre = 10;

    // reaproveita a implementação real (pura, sem I/O) em vez de um fake/mock separado — evita
    // qualquer drift de comportamento entre o que os testes exercitam e o que roda em produção
    private readonly IRankingService _rankingService = new RankingService();

    public List<Anuncio> Anuncios { get; } = new();

    public Task AdicionarAsync(Anuncio anuncio)
    {
        Anuncios.Add(anuncio);
        return Task.CompletedTask;
    }

    public Task<int> ContarAtivosPorVendedorAsync(Guid vendedorId)
    {
        var contagem = Anuncios.Count(a => a.VendedorId == vendedorId && a.Status == StatusAnuncio.Ativo);
        return Task.FromResult(contagem);
    }

    public Task<Anuncio?> ObterPorIdAsync(Guid id) => Task.FromResult(Anuncios.FirstOrDefault(a => a.Id == id));

    public Task AtualizarAsync(Anuncio anuncio) => Task.CompletedTask;

    public Task ExcluirAsync(Anuncio anuncio)
    {
        Anuncios.Remove(anuncio);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Anuncio>> ListarPorVendedorAsync(Guid vendedorId)
    {
        IReadOnlyList<Anuncio> lista = Anuncios
            .Where(a => a.VendedorId == vendedorId)
            .OrderByDescending(a => a.CriadoEm)
            .ToList();
        return Task.FromResult(lista);
    }

    public Task<IReadOnlyList<Anuncio>> BuscarAsync(FiltroBusca filtro)
    {
        var filtrados = Anuncios
            .Where(a => a.Status == StatusAnuncio.Ativo)
            .Where(a => filtro.Marca is null || (a.Marca?.Contains(filtro.Marca, StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(a => filtro.Modelo is null || (a.Modelo?.Contains(filtro.Modelo, StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(a => filtro.Versao is null || (a.Versao?.Contains(filtro.Versao, StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(a => filtro.Ano is null || a.Ano == filtro.Ano)
            .Where(a => filtro.Estado is null || string.Equals(a.Estado, filtro.Estado, StringComparison.OrdinalIgnoreCase))
            .Where(a => filtro.Cidade is null || string.Equals(a.Cidade, filtro.Cidade, StringComparison.OrdinalIgnoreCase))
            .Where(a => filtro.PrecoMin is null || a.Preco >= filtro.PrecoMin)
            .Where(a => filtro.PrecoMax is null || a.Preco <= filtro.PrecoMax);

        if (!string.IsNullOrWhiteSpace(filtro.TermoLivre))
        {
            var tokens = filtro.TermoLivre
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(MaxTokensTermoLivre);
            foreach (var token in tokens)
            {
                filtrados = filtrados.Where(a =>
                    (a.Marca?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.Modelo?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.Versao?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.Descricao?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false));
            }
        }

        IEnumerable<Anuncio> ordenados = filtro.Ordenacao switch
        {
            OrdenacaoBusca.PrecoAsc => filtrados.OrderBy(a => a.Preco).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            OrdenacaoBusca.PrecoDesc => filtrados.OrderByDescending(a => a.Preco).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            OrdenacaoBusca.AnoAsc => filtrados.OrderBy(a => a.Ano).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            OrdenacaoBusca.AnoDesc => filtrados.OrderByDescending(a => a.Ano).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            _ when _rankingService.PriorizaPatrocinado(filtro.Ordenacao) =>
                filtrados.OrderByDescending(a => a.Patrocinado).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            _ => filtrados.OrderByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
        };

        var pagina = Math.Max(filtro.Pagina, 1);
        var offset = (int)Math.Min((long)(pagina - 1) * TamanhoPagina, int.MaxValue);
        IReadOnlyList<Anuncio> lista = ordenados.Skip(offset).Take(TamanhoPagina).ToList();
        return Task.FromResult(lista);
    }

    public Task<Anuncio?> ObterAtivoPorIdAsync(Guid id) =>
        Task.FromResult(Anuncios.FirstOrDefault(a => a.Id == id && a.Status == StatusAnuncio.Ativo));

    public Task IncrementarVisualizacaoAsync(Guid id)
    {
        var anuncio = Anuncios.FirstOrDefault(a => a.Id == id);
        anuncio?.RegistrarVisualizacao();
        return Task.CompletedTask;
    }
}
