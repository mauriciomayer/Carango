namespace Carango.Infrastructure;

using Carango.Application;

// Placeholder explícito enquanto a Pergunta Aberta 3 do PRD (gateway de pagamento) não é
// respondida pelo cliente — não há gateway real pra chamar ainda. Sempre "aprova" a cobrança em
// produção (não existe motivo real de falha sem um gateway de verdade por trás); o construtor
// aceita um resultado fixo pra permitir simular falha em teste (AC #2 exige testar esse caminho).
// Quando o cliente decidir o gateway real, uma nova implementação de IBillingGateway substitui
// esta no registro de DI (Program.cs) — GerenciarAnuncioService não muda.
public class MockBillingGateway : IBillingGateway
{
    private readonly bool _sucesso;
    private readonly string? _motivoFalha;

    public MockBillingGateway(bool sucesso = true, string? motivoFalha = null)
    {
        _sucesso = sucesso;
        _motivoFalha = motivoFalha;
    }

    public Task<ResultadoCobranca> CobrarAsync(Guid vendedorId, string descricao, decimal valor) =>
        Task.FromResult(_sucesso
            ? new ResultadoCobranca(Sucesso: true)
            : new ResultadoCobranca(Sucesso: false, MotivoFalha: _motivoFalha ?? "Pagamento não autorizado."));
}
