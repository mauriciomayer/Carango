using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carango.Api.Contracts;
using Carango.Application;
using Carango.Domain;
using Carango.Infrastructure;
using Carango.Tests.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Carango.Tests.Api;

public class PlanosLojistaControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static WebApplicationFactory<Program> CriarFactory(
        FakeVendedorRepository vendedorRepositorio, FakePlanoLojistaRepository? planoRepositorio = null, FakeBillingGateway? billingGateway = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            JwtConfiguracaoTeste.Aplicar(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVendedorRepository>();
                services.AddSingleton<IVendedorRepository>(vendedorRepositorio);

                services.RemoveAll<IPlanoLojistaRepository>();
                services.AddSingleton<IPlanoLojistaRepository>(planoRepositorio ?? new FakePlanoLojistaRepository());

                services.RemoveAll<IBillingGateway>();
                services.AddSingleton<IBillingGateway>(billingGateway ?? new FakeBillingGateway());
            });
        });
    }

    // mesmo padrão de AnunciosControllerTests: JWT real (JwtGeradorToken de verdade), exercita
    // o pipeline [Authorize]/AddJwtBearer de ponta a ponta — diferente de lá, aqui o Vendedor
    // também precisa existir no FakeVendedorRepository, porque AssinarPlanoLojistaService
    // busca o Vendedor de verdade (não confia só nas claims) pra checar o Tipo
    private static (string Token, Vendedor Vendedor) GerarTokenParaVendedor(TipoVendedor tipo)
    {
        var vendedor = new Vendedor(
            $"vendedor-{Guid.NewGuid()}@exemplo.com", "hash-fake", tipo,
            cnpjRazaoSocial: tipo == TipoVendedor.Lojista ? "12.345.678/0001-90" : null);
        var geradorToken = new JwtGeradorToken(
            JwtConfiguracaoTeste.Issuer, JwtConfiguracaoTeste.Audience, JwtConfiguracaoTeste.SigningKey, TimeSpan.FromMinutes(60));
        var token = geradorToken.Gerar(vendedor);
        return (token.Token, vendedor);
    }

    [Fact]
    public async Task PostAssinar_ComLojistaSemPlano_Retorna201ComStatusAtivo()
    {
        var ct = TestContext.Current.CancellationToken;
        var vendedorRepositorio = new FakeVendedorRepository();
        var (token, lojista) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        vendedorRepositorio.Vendedores.Add(lojista);
        using var factory = CriarFactory(vendedorRepositorio);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/planos-lojista/assinar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var corpo = await response.Content.ReadFromJsonAsync<PlanoLojistaResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusPlanoLojista.Ativo);
    }

    [Fact]
    public async Task PostAssinar_ComPessoaFisica_Retorna403()
    {
        var ct = TestContext.Current.CancellationToken;
        var vendedorRepositorio = new FakeVendedorRepository();
        var (token, pessoaFisica) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        vendedorRepositorio.Vendedores.Add(pessoaFisica);
        using var factory = CriarFactory(vendedorRepositorio);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/planos-lojista/assinar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostAssinar_ComLojistaJaComPlanoAtivo_Retorna409()
    {
        var ct = TestContext.Current.CancellationToken;
        var vendedorRepositorio = new FakeVendedorRepository();
        var (token, lojista) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        vendedorRepositorio.Vendedores.Add(lojista);
        var planoRepositorio = new FakePlanoLojistaRepository();
        planoRepositorio.Planos.Add(PlanoLojista.Assinar(lojista.Id));
        using var factory = CriarFactory(vendedorRepositorio, planoRepositorio);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/planos-lojista/assinar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostAssinar_ComCobrancaRecusada_Retorna409()
    {
        var ct = TestContext.Current.CancellationToken;
        var vendedorRepositorio = new FakeVendedorRepository();
        var (token, lojista) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        vendedorRepositorio.Vendedores.Add(lojista);
        using var factory = CriarFactory(vendedorRepositorio, billingGateway: new FakeBillingGateway(sucesso: false, motivoFalha: "Cartão recusado."));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/planos-lojista/assinar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task PostAssinar_SemToken_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeVendedorRepository());
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/planos-lojista/assinar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMeu_ComPlanoAtivo_Retorna200()
    {
        var ct = TestContext.Current.CancellationToken;
        var (token, lojista) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        var planoRepositorio = new FakePlanoLojistaRepository();
        planoRepositorio.Planos.Add(PlanoLojista.Assinar(lojista.Id));
        using var factory = CriarFactory(new FakeVendedorRepository(), planoRepositorio);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/planos-lojista/meu", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<PlanoLojistaResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusPlanoLojista.Ativo);
    }

    [Fact]
    public async Task GetMeu_SemPlanoNenhum_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        using var factory = CriarFactory(new FakeVendedorRepository());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/planos-lojista/meu", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMeu_ComPlanoCancelado_Retorna200ComStatusCancelado()
    {
        // achado no code review: nenhum teste cobria esse caminho, embora seja exatamente o
        // mecanismo do qual a AC #3 (frontend) depende — ObterPorVendedorAsync não filtra por
        // Status, então um plano cancelado continua sendo "encontrado" (200), não "ausente" (404)
        var ct = TestContext.Current.CancellationToken;
        var (token, lojista) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        var planoRepositorio = new FakePlanoLojistaRepository();
        var plano = PlanoLojista.Assinar(lojista.Id);
        plano.Cancelar();
        planoRepositorio.Planos.Add(plano);
        using var factory = CriarFactory(new FakeVendedorRepository(), planoRepositorio);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/planos-lojista/meu", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<PlanoLojistaResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusPlanoLojista.Cancelado);
    }

    [Fact]
    public async Task GetMeu_SemToken_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeVendedorRepository());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/planos-lojista/meu", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostCancelar_ComPlanoAtivo_Retorna200ComStatusCancelado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (token, lojista) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        var planoRepositorio = new FakePlanoLojistaRepository();
        planoRepositorio.Planos.Add(PlanoLojista.Assinar(lojista.Id));
        using var factory = CriarFactory(new FakeVendedorRepository(), planoRepositorio);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/planos-lojista/cancelar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<PlanoLojistaResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusPlanoLojista.Cancelado);
    }

    [Fact]
    public async Task PostCancelar_SemPlanoNenhum_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        using var factory = CriarFactory(new FakeVendedorRepository());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/planos-lojista/cancelar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostCancelar_ComPlanoJaCancelado_Retorna409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (token, lojista) = GerarTokenParaVendedor(TipoVendedor.Lojista);
        var planoRepositorio = new FakePlanoLojistaRepository();
        var plano = PlanoLojista.Assinar(lojista.Id);
        plano.Cancelar();
        planoRepositorio.Planos.Add(plano);
        using var factory = CriarFactory(new FakeVendedorRepository(), planoRepositorio);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/planos-lojista/cancelar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostCancelar_SemToken_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeVendedorRepository());
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/planos-lojista/cancelar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
