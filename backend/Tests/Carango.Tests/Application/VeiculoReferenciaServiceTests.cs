using Carango.Application;
using Carango.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class VeiculoReferenciaServiceTests
{
    [Fact]
    public async Task ListarMarcasAsync_Sempre_RepassaParaOGateway()
    {
        var gateway = new FakeVeiculoReferenciaGateway();
        gateway.Marcas.Add(new VeiculoReferenciaItem("59", "VW - VolksWagen"));
        var service = new VeiculoReferenciaService(gateway);

        var resultado = await service.ListarMarcasAsync();

        resultado.ShouldHaveSingleItem();
        resultado[0].Nome.ShouldBe("VW - VolksWagen");
    }

    [Fact]
    public async Task ListarModelosAsync_Sempre_RepassaParaOGatewayComACodigoDaMarca()
    {
        var gateway = new FakeVeiculoReferenciaGateway();
        gateway.ModelosPorMarca["59"] = [new VeiculoReferenciaItem("5585", "AMAROK")];
        gateway.ModelosPorMarca["1"] = [new VeiculoReferenciaItem("100", "Integra")];
        var service = new VeiculoReferenciaService(gateway);

        var resultado = await service.ListarModelosAsync("59");

        resultado.ShouldHaveSingleItem();
        resultado[0].Nome.ShouldBe("AMAROK");
    }

    [Fact]
    public async Task ListarMarcasAsync_ComGatewayIndisponivel_PropagaAExcecao()
    {
        var gateway = new FakeVeiculoReferenciaGateway { LancarIndisponivel = true };
        var service = new VeiculoReferenciaService(gateway);

        await Should.ThrowAsync<VeiculoReferenciaIndisponivelException>(() => service.ListarMarcasAsync());
    }

    [Fact]
    public async Task ListarModelosAsync_ComGatewayIndisponivel_PropagaAExcecao()
    {
        var gateway = new FakeVeiculoReferenciaGateway { LancarIndisponivel = true };
        var service = new VeiculoReferenciaService(gateway);

        await Should.ThrowAsync<VeiculoReferenciaIndisponivelException>(() => service.ListarModelosAsync("59"));
    }
}
