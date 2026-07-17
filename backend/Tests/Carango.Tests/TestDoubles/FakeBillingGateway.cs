using Carango.Application;

namespace Carango.Tests.TestDoubles;

public class FakeBillingGateway : IBillingGateway
{
    private readonly bool _sucesso;
    private readonly string? _motivoFalha;

    public FakeBillingGateway(bool sucesso = true, string? motivoFalha = null)
    {
        _sucesso = sucesso;
        _motivoFalha = motivoFalha;
    }

    public List<(Guid VendedorId, string Descricao, decimal Valor)> Chamadas { get; } = new();

    public Task<ResultadoCobranca> CobrarAsync(Guid vendedorId, string descricao, decimal valor)
    {
        Chamadas.Add((vendedorId, descricao, valor));
        return Task.FromResult(_sucesso
            ? new ResultadoCobranca(Sucesso: true)
            : new ResultadoCobranca(Sucesso: false, MotivoFalha: _motivoFalha ?? "Pagamento não autorizado."));
    }
}
