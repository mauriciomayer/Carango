using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carango.Api.Contracts;
using Carango.Application;
using Carango.Domain;
using Carango.Tests.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Carango.Tests.Api;

public class VendedoresControllerTests
{
    // espelha a configuração real de Program.cs (JsonStringEnumConverter) — sem isso, o HttpClient de teste
    // usaria as opções padrão do .NET (enum como número), diferente do que o servidor realmente envia/aceita
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static WebApplicationFactory<Program> CriarFactory(FakeVendedorRepository repositorio)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // VendedoresController agora também depende de AutenticarVendedorService (Story 1.3), que
            // precisa de um Jwt:SigningKey válido só para o host subir — mesmo em testes que só chamam
            // Cadastrar. Ver JwtConfiguracaoTeste.
            JwtConfiguracaoTeste.Aplicar(builder);

            builder.ConfigureServices(services =>
            {
                // substitui os componentes reais de Infrastructure por fakes em memória — sem MySQL/Docker
                // real no ambiente de teste (ver Dev Notes da story sobre por que evitar isso aqui)
                services.RemoveAll<IVendedorRepository>();
                services.AddSingleton<IVendedorRepository>(repositorio);

                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
            });
        });
    }

    [Fact]
    public async Task PostVendedores_ComDadosValidos_Retorna201ComVendedorResponseSemSenha()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeVendedorRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var request = new CadastroVendedorRequest("marcos@exemplo.com", "senha-secreta", TipoVendedor.PessoaFisica);
        var response = await client.PostAsJsonAsync("/api/vendedores", request, JsonOptions, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var corpo = await response.Content.ReadAsStringAsync(ct);
        corpo.ShouldNotContain("senha-secreta");
        corpo.ShouldNotContain("SenhaHash", Case.Insensitive);

        var vendedorResponse = await response.Content.ReadFromJsonAsync<VendedorResponse>(JsonOptions, ct);
        vendedorResponse.ShouldNotBeNull();
        vendedorResponse!.Email.ShouldBe("marcos@exemplo.com");
        vendedorResponse.Tipo.ShouldBe(TipoVendedor.PessoaFisica);
        repositorio.Vendedores.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PostVendedores_ComEmailDuplicado_Retorna409ConflictComProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeVendedorRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var request = new CadastroVendedorRequest("marcos@exemplo.com", "senha-secreta", TipoVendedor.PessoaFisica);
        await client.PostAsJsonAsync("/api/vendedores", request, JsonOptions, ct);

        var response = await client.PostAsJsonAsync("/api/vendedores", request, JsonOptions, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        repositorio.Vendedores.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PostVendedores_LojistaSemCnpjRazaoSocial_Retorna400BadRequestComProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeVendedorRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var request = new CadastroVendedorRequest("loja@exemplo.com", "senha-secreta", TipoVendedor.Lojista);

        var response = await client.PostAsJsonAsync("/api/vendedores", request, JsonOptions, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        repositorio.Vendedores.ShouldBeEmpty();
    }
}
