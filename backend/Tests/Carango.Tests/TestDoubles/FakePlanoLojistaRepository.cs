using Carango.Application;
using Carango.Domain;

namespace Carango.Tests.TestDoubles;

public class FakePlanoLojistaRepository : IPlanoLojistaRepository
{
    public List<PlanoLojista> Planos { get; } = new();

    public Task<PlanoLojista?> ObterPorVendedorAsync(Guid vendedorId)
    {
        var plano = Planos.SingleOrDefault(p => p.VendedorId == vendedorId);
        return Task.FromResult(plano);
    }

    public Task<bool> TemPlanoAtivoAsync(Guid vendedorId)
    {
        var temAtivo = Planos.Any(p => p.VendedorId == vendedorId && p.Status == StatusPlanoLojista.Ativo);
        return Task.FromResult(temAtivo);
    }

    public Task AdicionarAsync(PlanoLojista plano)
    {
        Planos.Add(plano);
        return Task.CompletedTask;
    }

    // mesma referência de objeto já está na lista Planos — mesmo padrão de FakeAnuncioRepository
    public Task AtualizarAsync(PlanoLojista plano) => Task.CompletedTask;
}
