namespace Carango.Application;

// única fronteira pra integração externa (AD-12) — implementada em Infrastructure contra a API
// pública da Fipe (Story 2.6); o frontend nunca conhece a existência do serviço externo
public interface IVeiculoReferenciaGateway
{
    Task<IReadOnlyList<VeiculoReferenciaItem>> ListarMarcasAsync();

    Task<IReadOnlyList<VeiculoReferenciaItem>> ListarModelosAsync(string marcaCodigo);
}
