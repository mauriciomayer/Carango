using Carango.Domain;

namespace Carango.Application;

public interface IPlanoLojistaRepository
{
    Task<PlanoLojista?> ObterPorVendedorAsync(Guid vendedorId);

    Task<bool> TemPlanoAtivoAsync(Guid vendedorId);

    Task AdicionarAsync(PlanoLojista plano);

    Task AtualizarAsync(PlanoLojista plano);
}
