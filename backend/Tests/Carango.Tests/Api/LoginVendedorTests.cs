using System.Net;
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
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Carango.Tests.Api;

public class LoginVendedorTests
{
    private const string ChaveTeste = JwtConfiguracaoTeste.SigningKey;
    private const string IssuerTeste = JwtConfiguracaoTeste.Issuer;
    private const string AudienceTeste = JwtConfiguracaoTeste.Audience;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Repositório é o único componente substituído por fake — IPasswordHasher e IGeradorToken continuam
    // as implementações reais registradas por AddInfrastructure, para provar que um JWT de verdade,
    // válido e assinado, sai do endpoint (AC #1 exige "token JWT válido", um fake não prova isso).
    private static WebApplicationFactory<Program> CriarFactory(FakeVendedorRepository repositorio)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            JwtConfiguracaoTeste.Aplicar(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVendedorRepository>();
                services.AddSingleton<IVendedorRepository>(repositorio);
            });
        });
    }

    private static async Task<Vendedor> AdicionarVendedorComSenhaHasheadaAsync(FakeVendedorRepository repositorio, string email, string senha)
    {
        // usa o PasswordHasher real (não o fake) — precisa ser um hash de verdade pra Verificar() bater no login
        var hasher = new PasswordHasher();
        var vendedor = new Vendedor(email, hasher.Hash(senha), TipoVendedor.PessoaFisica);
        await repositorio.AdicionarAsync(vendedor);
        return vendedor;
    }

    [Fact]
    public async Task PostLogin_ComCredenciaisCorretas_Retorna200ComTokenJwtValido()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeVendedorRepository();
        var vendedor = await AdicionarVendedorComSenhaHasheadaAsync(repositorio, "marcos@exemplo.com", "senha-secreta");
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/vendedores/login", new LoginVendedorRequest("marcos@exemplo.com", "senha-secreta"), JsonOptions, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<LoginVendedorResponse>(JsonOptions, ct);
        corpo.ShouldNotBeNull();
        corpo!.Vendedor.Email.ShouldBe("marcos@exemplo.com");
        corpo.ExpiraEm.ShouldBeGreaterThan(DateTime.UtcNow);

        var handler = new JsonWebTokenHandler();
        var resultado = await handler.ValidateTokenAsync(corpo.Token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = IssuerTeste,
            ValidateAudience = true,
            ValidAudience = AudienceTeste,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(ChaveTeste))
        });

        resultado.IsValid.ShouldBeTrue(resultado.Exception?.ToString());
        resultado.ClaimsIdentity.FindFirst("sub")!.Value.ShouldBe(vendedor.Id.ToString());
    }

    [Fact]
    public async Task PostLogin_ComSenhaIncorreta_Retorna401ComMensagemGenerica()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeVendedorRepository();
        await AdicionarVendedorComSenhaHasheadaAsync(repositorio, "marcos@exemplo.com", "senha-secreta");
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/vendedores/login", new LoginVendedorRequest("marcos@exemplo.com", "senha-errada"), JsonOptions, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task PostLogin_ComEmailNaoCadastrado_Retorna401ComMesmaMensagemDeSenhaIncorreta()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorioComConta = new FakeVendedorRepository();
        await AdicionarVendedorComSenhaHasheadaAsync(repositorioComConta, "marcos@exemplo.com", "senha-secreta");
        using var factoryComConta = CriarFactory(repositorioComConta);
        using var clientComConta = factoryComConta.CreateClient();

        var respostaSenhaErrada = await clientComConta.PostAsJsonAsync(
            "/api/vendedores/login", new LoginVendedorRequest("marcos@exemplo.com", "senha-errada"), JsonOptions, ct);

        using var factorySemConta = CriarFactory(new FakeVendedorRepository());
        using var clientSemConta = factorySemConta.CreateClient();

        var respostaEmailDesconhecido = await clientSemConta.PostAsJsonAsync(
            "/api/vendedores/login", new LoginVendedorRequest("desconhecido@exemplo.com", "qualquer-senha"), JsonOptions, ct);

        respostaEmailDesconhecido.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // compara só title/detail/status — ASP.NET Core inclui um traceId por requisição no Problem Details,
        // que nunca vai bater entre duas chamadas distintas, mesmo quando a mensagem de negócio é idêntica
        var problemaSenhaErrada = await respostaSenhaErrada.Content.ReadFromJsonAsync<ProblemDetailsSemTraceId>(ct);
        var problemaEmailDesconhecido = await respostaEmailDesconhecido.Content.ReadFromJsonAsync<ProblemDetailsSemTraceId>(ct);
        problemaSenhaErrada.ShouldBe(problemaEmailDesconhecido);
    }

    private record ProblemDetailsSemTraceId(string? Title, string? Detail, int? Status);
}
