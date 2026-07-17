using Carango.Domain;

namespace Carango.Application;

public interface IVendedorRepository
{
    Task<bool> ExisteEmailAsync(string email);

    Task<Vendedor?> ObterPorEmailAsync(string email);

    Task<Vendedor?> ObterPorIdAsync(Guid id);

    Task AdicionarAsync(Vendedor vendedor);
}
