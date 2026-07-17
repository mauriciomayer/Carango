using System.Net;
using System.Net.Http.Json;
using Carango.Api.Contracts;
using Carango.Application;
using Carango.Tests.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Carango.Tests.Api;

public class VeiculosReferenciaControllerTests
{
    private static WebApplicationFactory<Program> CriarFactory(FakeVeiculoReferenciaGateway gateway)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            JwtConfiguracaoTeste.Aplicar(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVeiculoReferenciaGateway>();
                services.AddSingleton<IVeiculoReferenciaGateway>(gateway);
            });
        });
    }

    [Fact]
    public async Task GetMarcas_SemAuthorizationHeader_Retorna200()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeVeiculoReferenciaGateway();
        gateway.Marcas.Add(new VeiculoReferenciaItem("59", "VW - VolksWagen"));
        using var factory = CriarFactory(gateway);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/veiculos-referencia/marcas", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<List<VeiculoReferenciaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Nome.ShouldBe("VW - VolksWagen");
    }

    [Fact]
    public async Task GetModelos_ComMarcaValida_RetornaSoOsModelosDaquelaMarca()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeVeiculoReferenciaGateway();
        gateway.ModelosPorMarca["59"] = [new VeiculoReferenciaItem("5585", "AMAROK")];
        gateway.ModelosPorMarca["1"] = [new VeiculoReferenciaItem("100", "Integra")];
        using var factory = CriarFactory(gateway);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/veiculos-referencia/modelos?marca=59", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<List<VeiculoReferenciaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Nome.ShouldBe("AMAROK");
    }

    [Fact]
    public async Task GetModelos_SemParametroMarca_Retorna400()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeVeiculoReferenciaGateway();
        using var factory = CriarFactory(gateway);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/veiculos-referencia/modelos", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("59; DROP TABLE")]
    [InlineData("../etc")]
    [InlineData("http://evil.com")]
    public async Task GetModelos_ComMarcaNaoNumerica_Retorna400(string marca)
    {
        // achado no code review: código de marca da Fipe é sempre numérico — qualquer outro
        // formato é rejeitado aqui antes de virar segmento de path na chamada pra Fipe
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeVeiculoReferenciaGateway();
        using var factory = CriarFactory(gateway);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/veiculos-referencia/modelos?marca={Uri.EscapeDataString(marca)}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetMarcas_ComFipeIndisponivel_Retorna503()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeVeiculoReferenciaGateway { LancarIndisponivel = true };
        using var factory = CriarFactory(gateway);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/veiculos-referencia/marcas", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetModelos_ComFipeIndisponivel_Retorna503()
    {
        var ct = TestContext.Current.CancellationToken;
        var gateway = new FakeVeiculoReferenciaGateway { LancarIndisponivel = true };
        using var factory = CriarFactory(gateway);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/veiculos-referencia/modelos?marca=59", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}
