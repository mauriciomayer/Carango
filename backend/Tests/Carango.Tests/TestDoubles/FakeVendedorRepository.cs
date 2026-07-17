using Carango.Application;
using Carango.Domain;

namespace Carango.Tests.TestDoubles;

public class FakeVendedorRepository : IVendedorRepository
{
    public List<Vendedor> Vendedores { get; } = new();

    public Task<bool> ExisteEmailAsync(string email)
    {
        var existe = Vendedores.Any(v => v.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(existe);
    }

    public Task<Vendedor?> ObterPorEmailAsync(string email)
    {
        var vendedor = Vendedores.SingleOrDefault(v => v.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(vendedor);
    }

    public Task<Vendedor?> ObterPorIdAsync(Guid id)
    {
        var vendedor = Vendedores.SingleOrDefault(v => v.Id == id);
        return Task.FromResult(vendedor);
    }

    public Task AdicionarAsync(Vendedor vendedor)
    {
        Vendedores.Add(vendedor);
        return Task.CompletedTask;
    }
}
