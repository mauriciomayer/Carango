using Carango.Application;
using Carango.Domain;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Carango.Infrastructure;

public class VendedorRepository : IVendedorRepository
{
    private readonly CarangoDbContext _dbContext;

    public VendedorRepository(CarangoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExisteEmailAsync(string email) =>
        await _dbContext.Vendedores.AnyAsync(v => v.Email == email);

    public async Task<Vendedor?> ObterPorEmailAsync(string email) =>
        // AsNoTracking — leitura pura pra checagem de credencial no login, nunca salva de volta
        await _dbContext.Vendedores.AsNoTracking().SingleOrDefaultAsync(v => v.Email == email);

    public async Task<Vendedor?> ObterPorIdAsync(Guid id) =>
        // AsNoTracking — mesma leitura pura da checagem de Tipo em AssinarPlanoLojistaService, nunca salva de volta
        await _dbContext.Vendedores.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id);

    public async Task AdicionarAsync(Vendedor vendedor)
    {
        _dbContext.Vendedores.Add(vendedor);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            // corrida entre duas requisições simultâneas de cadastro com o mesmo e-mail — a checagem em
            // ExisteEmailAsync não fecha essa janela sozinha; o índice único (IX_Vendedores_Email) é quem
            // garante a invariante, e aqui traduzimos a violação dele para o mesmo erro de negócio esperado
            throw new EmailJaCadastradoException();
        }
    }
}
