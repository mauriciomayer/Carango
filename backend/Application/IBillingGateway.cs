namespace Carango.Application;

// Definida uma única vez (AD-4/AD-11) — a Story 4.2 (assinatura de Plano Lojista) reaproveita esta
// mesma interface quando chegar a vez, não cria uma segunda. Nenhum SDK de gateway de pagamento é
// referenciado fora de Infrastructure — hoje só existe MockBillingGateway (Pergunta Aberta 3 do
// PRD, gateway real ainda não definido pelo cliente)
public interface IBillingGateway
{
    Task<ResultadoCobranca> CobrarAsync(Guid vendedorId, string descricao, decimal valor);
}
