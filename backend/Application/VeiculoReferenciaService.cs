namespace Carango.Application;

// passthrough fino pro gateway — mesmo padrão de BuscarAnunciosService.BuscarAsync (Controllers
// deste projeto nunca falam com Infrastructure/gateway direto, sempre por um Service de
// Application, mesmo quando não há lógica de negócio nenhuma além do repasse)
public class VeiculoReferenciaService
{
    private readonly IVeiculoReferenciaGateway _gateway;

    public VeiculoReferenciaService(IVeiculoReferenciaGateway gateway)
    {
        _gateway = gateway;
    }

    public Task<IReadOnlyList<VeiculoReferenciaItem>> ListarMarcasAsync() => _gateway.ListarMarcasAsync();

    public Task<IReadOnlyList<VeiculoReferenciaItem>> ListarModelosAsync(string marcaCodigo) =>
        _gateway.ListarModelosAsync(marcaCodigo);
}
