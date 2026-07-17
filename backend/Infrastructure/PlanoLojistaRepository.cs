using Carango.Application;
using Carango.Domain;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Carango.Infrastructure;

public class PlanoLojistaRepository : IPlanoLojistaRepository
{
    private readonly CarangoDbContext _dbContext;

    public PlanoLojistaRepository(CarangoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlanoLojista?> ObterPorVendedorAsync(Guid vendedorId) =>
        await _dbContext.PlanosLojista.SingleOrDefaultAsync(p => p.VendedorId == vendedorId);

    public async Task<bool> TemPlanoAtivoAsync(Guid vendedorId) =>
        await _dbContext.PlanosLojista.AnyAsync(p => p.VendedorId == vendedorId && p.Status == StatusPlanoLojista.Ativo);

    public async Task AdicionarAsync(PlanoLojista plano)
    {
        _dbContext.PlanosLojista.Add(plano);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            // corrida entre duas chamadas simultâneas de "assinar" do mesmo Vendedor — a checagem de
            // idempotência em AssinarPlanoLojistaService (ObterPorVendedorAsync antes de cobrar) não
            // fecha essa janela sozinha; o índice único em VendedorId (PlanoLojistaConfiguration) é
            // quem garante a invariante de fato. Achado no code review: sem essa tradução, a chamada
            // perdedora da corrida virava um DbUpdateException não tratado (500) em vez de um 409
            // controlado — mesmo padrão já usado historicamente em AnuncioRepository/VendedorRepository
            throw new PlanoLojistaJaAtivoException();
        }
    }

    // nenhum Update()/Attach() explícito é necessário — a entidade já está rastreada desde que veio
    // de ObterPorVendedorAsync no mesmo DbContext. Cancelar() nunca muda VendedorId, então não pode
    // violar o índice único ali — sem tradução de DbUpdateException necessária aqui
    public async Task AtualizarAsync(PlanoLojista plano) =>
        await _dbContext.SaveChangesAsync();
}
