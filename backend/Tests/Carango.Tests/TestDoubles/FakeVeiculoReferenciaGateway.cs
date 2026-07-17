using Carango.Application;

namespace Carango.Tests.TestDoubles;

public class FakeVeiculoReferenciaGateway : IVeiculoReferenciaGateway
{
    public List<VeiculoReferenciaItem> Marcas { get; } = new();

    public Dictionary<string, List<VeiculoReferenciaItem>> ModelosPorMarca { get; } = new();

    // simula a Fipe fora do ar/timeout — mesmo padrão de outros fakes que simulam falha
    // (RepositorioQueFalhaAoIncrementarVisualizacao, Story 4.5)
    public bool LancarIndisponivel { get; set; }

    public Task<IReadOnlyList<VeiculoReferenciaItem>> ListarMarcasAsync()
    {
        if (LancarIndisponivel) throw new VeiculoReferenciaIndisponivelException();
        return Task.FromResult<IReadOnlyList<VeiculoReferenciaItem>>(Marcas);
    }

    public Task<IReadOnlyList<VeiculoReferenciaItem>> ListarModelosAsync(string marcaCodigo)
    {
        if (LancarIndisponivel) throw new VeiculoReferenciaIndisponivelException();
        var modelos = ModelosPorMarca.TryGetValue(marcaCodigo, out var lista) ? lista : new List<VeiculoReferenciaItem>();
        return Task.FromResult<IReadOnlyList<VeiculoReferenciaItem>>(modelos);
    }
}
