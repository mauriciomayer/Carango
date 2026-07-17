using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

public class AnunciosControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static WebApplicationFactory<Program> CriarFactory(
        FakeAnuncioRepository repositorio, FakeMediaStorage? mediaStorage = null, FakeBillingGateway? billingGateway = null,
        FakeVendedorRepository? vendedorRepositorio = null, FakePlanoLojistaRepository? planoRepositorio = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            JwtConfiguracaoTeste.Aplicar(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnuncioRepository>();
                services.AddSingleton<IAnuncioRepository>(repositorio);

                services.RemoveAll<IMediaStorage>();
                services.AddSingleton<IMediaStorage>(mediaStorage ?? new FakeMediaStorage());

                services.RemoveAll<IBillingGateway>();
                services.AddSingleton<IBillingGateway>(billingGateway ?? new FakeBillingGateway());

                // Story 4.2 — GerenciarAnuncioService.ReativarAsync passou a depender dessas duas
                // interfaces (isenção do limite pra Lojista com Plano Lojista ativo). Sem sobrescrever
                // aqui, caem nas implementações reais de Infrastructure e tentam conectar num MySQL de
                // verdade — achado no code review desta story, os testes deste arquivo não deveriam
                // depender de infraestrutura externa
                services.RemoveAll<IVendedorRepository>();
                services.AddSingleton<IVendedorRepository>(vendedorRepositorio ?? new FakeVendedorRepository());

                services.RemoveAll<IPlanoLojistaRepository>();
                services.AddSingleton<IPlanoLojistaRepository>(planoRepositorio ?? new FakePlanoLojistaRepository());
            });
        });
    }

    // gera um JWT real (JwtGeradorToken de verdade, não fake) pra simular um Vendedor autenticado —
    // exercita o pipeline [Authorize]/AddJwtBearer de ponta a ponta, incluindo o MapInboundClaims = false
    private static (string Token, Guid VendedorId) GerarTokenParaVendedor(TipoVendedor tipo)
    {
        var vendedor = new Vendedor(
            $"vendedor-{Guid.NewGuid()}@exemplo.com", "hash-fake", tipo,
            cnpjRazaoSocial: tipo == TipoVendedor.Lojista ? "12.345.678/0001-90" : null);
        var geradorToken = new JwtGeradorToken(
            JwtConfiguracaoTeste.Issuer, JwtConfiguracaoTeste.Audience, JwtConfiguracaoTeste.SigningKey, TimeSpan.FromMinutes(60));
        var token = geradorToken.Gerar(vendedor);
        return (token.Token, vendedor.Id);
    }

    private static MultipartFormDataContent ConteudoFichaCompleta(bool publicar)
    {
        return new MultipartFormDataContent
        {
            { new StringContent("Honda"), "Marca" },
            { new StringContent("Civic"), "Modelo" },
            { new StringContent("2019"), "Ano" },
            { new StringContent("EXL"), "Versao" },
            { new StringContent("95000"), "Preco" },
            { new StringContent("Único dono"), "Descricao" },
            { new StringContent("SP"), "Estado" },
            { new StringContent("São Paulo"), "Cidade" },
            { new StringContent(publicar.ToString()), "Publicar" },
        };
    }

    private static void AdicionarFotoFake(MultipartFormDataContent conteudo, string nomeArquivo, byte[]? bytes = null)
    {
        var arquivo = new ByteArrayContent(bytes ?? Encoding.UTF8.GetBytes("conteudo-fake-de-imagem"));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        conteudo.Add(arquivo, "Fotos", nomeArquivo);
    }

    [Fact]
    public async Task PostAnuncios_ComFichaCompletaEPublicarTrue_Retorna201ComStatusAtivo()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/anuncios", ConteudoFichaCompleta(publicar: true), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo.ShouldNotBeNull();
        corpo!.Status.ShouldBe(StatusAnuncio.Ativo);
        corpo.VendedorId.ShouldBe(vendedorId);
        repositorio.Anuncios.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PostAnuncios_ComCampoObrigatorioAusenteEPublicarTrue_Retorna400()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = new MultipartFormDataContent
        {
            { new StringContent("Honda"), "Marca" },
            { new StringContent("true"), "Publicar" },
        };

        var response = await client.PostAsync("/api/anuncios", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        repositorio.Anuncios.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostAnuncios_ComFotoDeZeroBytes_Retorna400()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = ConteudoFichaCompleta(publicar: true);
        AdicionarFotoFake(conteudo, "foto-vazia.jpg", bytes: []);

        var response = await client.PostAsync("/api/anuncios", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        repositorio.Anuncios.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostAnuncios_ComCamposIncompletosEPublicarFalse_Retorna201ComStatusRascunho()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = new MultipartFormDataContent
        {
            { new StringContent("Honda"), "Marca" },
            { new StringContent("false"), "Publicar" },
        };

        var response = await client.PostAsync("/api/anuncios", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusAnuncio.Rascunho);
    }

    [Fact]
    public async Task PostAnuncios_SemTokenAuthorization_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/anuncios", ConteudoFichaCompleta(publicar: true), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostAnuncios_SegundaTentativaDePublicarComUmAtivoExistente_Retorna409()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await client.PostAsync("/api/anuncios", ConteudoFichaCompleta(publicar: true), ct);

        var response = await client.PostAsync("/api/anuncios", ConteudoFichaCompleta(publicar: true), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        repositorio.Anuncios.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PostAnuncios_ComMultiplasFotos_TodasAssociadasNaOrdemEnviada()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var mediaStorage = new FakeMediaStorage();
        using var factory = CriarFactory(repositorio, mediaStorage);
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = ConteudoFichaCompleta(publicar: true);
        AdicionarFotoFake(conteudo, "foto1.jpg");
        AdicionarFotoFake(conteudo, "foto2.jpg");

        var response = await client.PostAsync("/api/anuncios", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Fotos.Count.ShouldBe(2);
        mediaStorage.ArquivosSalvos.Count.ShouldBe(2);
    }

    private static Anuncio CriarAnuncioDoVendedor(Guid vendedorId) =>
        Anuncio.CriarRascunho(
            vendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");

    private static EditarAnuncioRequest EdicaoValida() =>
        new("Toyota", "Corolla", 2021, "XEI", 110000m, "Revisado", "RJ", "Rio de Janeiro");

    [Fact]
    public async Task GetAnuncio_DoDono_Retorna200ComOsDados()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/anuncios/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Marca.ShouldBe("Honda");
    }

    [Fact]
    public async Task GetAnuncio_ComVisualizacoesRegistradas_RetornaOContadorCorreto()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        await repositorio.IncrementarVisualizacaoAsync(anuncio.Id);
        await repositorio.IncrementarVisualizacaoAsync(anuncio.Id);
        await repositorio.IncrementarVisualizacaoAsync(anuncio.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/anuncios/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Visualizacoes.ShouldBe(3);
    }

    [Fact]
    public async Task GetAnuncio_SemNenhumaVisualizacao_RetornaContadorZerado()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/anuncios/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Visualizacoes.ShouldBe(0);
    }

    [Fact]
    public async Task GetAnuncio_DeOutroVendedor_Retorna403()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var anuncio = CriarAnuncioDoVendedor(Guid.NewGuid());
        await repositorio.AdicionarAsync(anuncio);
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/anuncios/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetAnuncio_ComIdInexistente_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/anuncios/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetAnuncio_SemTokenAuthorization_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/anuncios/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutAnuncio_DoDonoComCamposValidos_Retorna200EAtualiza()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/anuncios/{anuncio.Id}", EdicaoValida(), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Marca.ShouldBe("Toyota");
        corpo.Cidade.ShouldBe("Rio de Janeiro");
    }

    [Fact]
    public async Task PutAnuncio_DeOutroVendedor_Retorna403()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var anuncio = CriarAnuncioDoVendedor(Guid.NewGuid());
        await repositorio.AdicionarAsync(anuncio);
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/anuncios/{anuncio.Id}", EdicaoValida(), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        anuncio.Marca.ShouldBe("Honda");
    }

    [Fact]
    public async Task PutAnuncio_ComIdInexistente_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/anuncios/{Guid.NewGuid()}", EdicaoValida(), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutAnuncio_SemTokenAuthorization_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/anuncios/{Guid.NewGuid()}", EdicaoValida(), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutAnuncio_EmAnuncioAtivoRemovendoCampoObrigatorio_Retorna400ENadaAlterado()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var edicaoInvalida = EdicaoValida() with { Marca = null };

        var response = await client.PutAsJsonAsync($"/api/anuncios/{anuncio.Id}", edicaoInvalida, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        anuncio.Marca.ShouldBe("Honda");
    }

    [Fact]
    public async Task PutAnuncio_EmRascunhoComAnoEPrecoNulos_Retorna200EAplicaOsCampos()
    {
        // regressão: o formulário de edição envia Ano/Preco como number|null no JSON (nunca "" —
        // uma string vazia não é um número JSON válido e quebraria o binding de int?/decimal? antes
        // mesmo de chegar na validação de domínio, achado independente pelas 3 camadas de code review)
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var corpoCru = new StringContent(
            "{\"marca\":\"Toyota\",\"modelo\":\"Corolla\",\"ano\":null,\"versao\":\"XEI\",\"preco\":null,\"descricao\":\"Revisado\",\"estado\":\"RJ\",\"cidade\":\"Rio de Janeiro\"}",
            Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/api/anuncios/{anuncio.Id}", corpoCru, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Ano.ShouldBeNull();
        corpo.Preco.ShouldBeNull();
        corpo.Marca.ShouldBe("Toyota");
    }

    [Fact]
    public async Task PostPausar_DeAtivo_Retorna200ComStatusPausado()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/pausar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusAnuncio.Pausado);
    }

    [Fact]
    public async Task PostPausar_DeRascunho_Retorna409()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/pausar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task PostReativar_DeAnuncioJaAtivo_Retorna409()
    {
        // regressão: sem a correção de ordenação em ReativarAsync, este cenário virava 409 por
        // "limite excedido" (mensagem enganosa) em vez de 409 por transição inválida — o código de
        // status já batia com a AC #5, mas a classificação/mensagem estavam erradas. Achado no code review.
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/reativar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var corpo = await response.Content.ReadAsStringAsync(ct);
        corpo.ShouldNotContain("Você já tem um Anúncio ativo");
    }

    [Fact]
    public async Task PostMarcarVendido_DeRascunho_Retorna409()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/marcar-vendido", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task PostReativar_DePausadoSemOutroAtivo_Retorna200ComStatusAtivo()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        anuncio.Pausar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/reativar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusAnuncio.Ativo);
    }

    [Fact]
    public async Task PostReativar_ComOutroAnuncioAtivoDoMesmoVendedor_Retorna409()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var pausado = CriarAnuncioDoVendedor(vendedorId);
        pausado.Publicar();
        pausado.Pausar();
        await repositorio.AdicionarAsync(pausado);
        var outroAtivo = CriarAnuncioDoVendedor(vendedorId);
        outroAtivo.Publicar();
        await repositorio.AdicionarAsync(outroAtivo);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{pausado.Id}/reativar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        pausado.Status.ShouldBe(StatusAnuncio.Pausado);
    }

    [Fact]
    public async Task PostMarcarVendido_DeAtivo_Retorna200ComStatusVendido()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/marcar-vendido", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusAnuncio.Vendido);
    }

    [Fact]
    public async Task PostMarcarVendido_DePausado_Retorna200ComStatusVendido()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        anuncio.Pausar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/marcar-vendido", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Status.ShouldBe(StatusAnuncio.Vendido);
    }

    [Theory]
    [InlineData("pausar")]
    [InlineData("reativar")]
    [InlineData("marcar-vendido")]
    public async Task PostTransicaoDeStatus_DeOutroVendedor_Retorna403(string acao)
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var anuncio = CriarAnuncioDoVendedor(Guid.NewGuid());
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/{acao}", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("pausar")]
    [InlineData("reativar")]
    [InlineData("marcar-vendido")]
    public async Task PostTransicaoDeStatus_ComIdInexistente_Retorna404(string acao)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{Guid.NewGuid()}/{acao}", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("pausar")]
    [InlineData("reativar")]
    [InlineData("marcar-vendido")]
    public async Task PostTransicaoDeStatus_SemTokenAuthorization_Retorna401(string acao)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/anuncios/{Guid.NewGuid()}/{acao}", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FluxoCompleto_MarcarAtivoComoVendidoLiberaCotaParaPublicarNovoAtivo()
    {
        // ponta a ponta da AC #3: publicar um Anúncio ativo (limite atingido) → marcar como vendido →
        // publicar um segundo Anúncio ativo com sucesso, provando que a cota realmente libera
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var primeiraResposta = await client.PostAsync("/api/anuncios", ConteudoFichaCompleta(publicar: true), ct);
        var primeiroAnuncio = await primeiraResposta.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);

        var bloqueado = await client.PostAsync("/api/anuncios", ConteudoFichaCompleta(publicar: true), ct);
        bloqueado.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var respostaVendido = await client.PostAsync($"/api/anuncios/{primeiroAnuncio!.Id}/marcar-vendido", null, ct);
        respostaVendido.StatusCode.ShouldBe(HttpStatusCode.OK);

        var segundaResposta = await client.PostAsync("/api/anuncios", ConteudoFichaCompleta(publicar: true), ct);

        segundaResposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        repositorio.Anuncios.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DeleteAnuncio_DoDono_Retorna204EExclui()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/anuncios/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        repositorio.Anuncios.ShouldNotContain(anuncio);
    }

    [Fact]
    public async Task DeleteAnuncio_DeOutroVendedor_Retorna403ENaoExclui()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var anuncio = CriarAnuncioDoVendedor(Guid.NewGuid());
        await repositorio.AdicionarAsync(anuncio);
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/anuncios/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        repositorio.Anuncios.ShouldContain(anuncio);
    }

    [Fact]
    public async Task DeleteAnuncio_ComIdInexistente_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/anuncios/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task DeleteAnuncio_SemTokenAuthorization_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/anuncios/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(StatusAnuncio.Rascunho)]
    [InlineData(StatusAnuncio.Ativo)]
    [InlineData(StatusAnuncio.Pausado)]
    [InlineData(StatusAnuncio.Vendido)]
    public async Task DeleteAnuncio_EmQualquerStatus_Retorna204(StatusAnuncio status)
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        if (status != StatusAnuncio.Rascunho) anuncio.Publicar();
        if (status == StatusAnuncio.Pausado) anuncio.Pausar();
        if (status == StatusAnuncio.Vendido) anuncio.MarcarComoVendido();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/anuncios/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetAnuncios_ComTresAnunciosDoVendedor_Retorna200OrdenadosDoMaisRecenteProMaisAntigo()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var rascunho = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(rascunho);
        Thread.Sleep(20);
        var ativo = CriarAnuncioDoVendedor(vendedorId);
        ativo.Publicar();
        await repositorio.AdicionarAsync(ativo);
        Thread.Sleep(20);
        var pausado = CriarAnuncioDoVendedor(vendedorId);
        pausado.Publicar();
        pausado.Pausar();
        await repositorio.AdicionarAsync(pausado);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/anuncios", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<List<AnuncioResponse>>(JsonOptions, ct);
        lista!.Count.ShouldBe(3);
        lista[0].Id.ShouldBe(pausado.Id);
        lista[1].Id.ShouldBe(ativo.Id);
        lista[2].Id.ShouldBe(rascunho.Id);
    }

    [Fact]
    public async Task GetAnuncios_ComVisualizacoesRegistradas_RetornaOContadorCorreto()
    {
        // achado no code review (Blind Hunter): os testes de Visualizacoes cobriam só GET /api/anuncios/{id}
        // (Obter), mas o Painel do Lojista consome GET /api/anuncios (Listar) via listarMeusAnuncios() —
        // os dois endpoints compartilham ParaResponse hoje, mas nada garantia isso continuar assim
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        await repositorio.IncrementarVisualizacaoAsync(anuncio.Id);
        await repositorio.IncrementarVisualizacaoAsync(anuncio.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/anuncios", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<List<AnuncioResponse>>(JsonOptions, ct);
        lista!.ShouldHaveSingleItem();
        lista[0].Visualizacoes.ShouldBe(2);
    }

    [Fact]
    public async Task GetAnuncios_ComAnuncioVendido_IncluiNaLista()
    {
        // regressão: AC #1 exige que a listagem inclua os 4 status (Rascunho/Ativo/Pausado/Vendido) —
        // o teste de ordenação acima só cobria os 3 primeiros, achado no code review (Acceptance Auditor)
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var vendido = CriarAnuncioDoVendedor(vendedorId);
        vendido.Publicar();
        vendido.MarcarComoVendido();
        await repositorio.AdicionarAsync(vendido);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/anuncios", ct);

        var lista = await response.Content.ReadFromJsonAsync<List<AnuncioResponse>>(JsonOptions, ct);
        lista!.ShouldHaveSingleItem();
        lista[0].Status.ShouldBe(StatusAnuncio.Vendido);
    }

    [Fact]
    public async Task GetAnuncios_SemNenhumAnuncio_Retorna200ComListaVazia()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/anuncios", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<List<AnuncioResponse>>(JsonOptions, ct);
        lista.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAnuncios_SoRetornaOsDoVendedorAutenticado()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var doVendedor = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(doVendedor);
        var deOutroVendedor = CriarAnuncioDoVendedor(Guid.NewGuid());
        await repositorio.AdicionarAsync(deOutroVendedor);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/anuncios", ct);

        var lista = await response.Content.ReadFromJsonAsync<List<AnuncioResponse>>(JsonOptions, ct);
        lista!.ShouldHaveSingleItem();
        lista[0].Id.ShouldBe(doVendedor.Id);
    }

    [Fact]
    public async Task GetAnuncios_SemTokenAuthorization_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/anuncios", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostDestacar_DeAtivoComCobrancaAprovada_Retorna200ComPatrocinadoTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio, billingGateway: new FakeBillingGateway());
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/destacar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Patrocinado.ShouldBeTrue();
    }

    [Fact]
    public async Task PostDestacar_ComCobrancaRecusada_Retorna409SemMarcarComoPatrocinado()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio, billingGateway: new FakeBillingGateway(sucesso: false, motivoFalha: "Cartão recusado."));
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/destacar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        anuncio.Patrocinado.ShouldBeFalse();
    }

    [Fact]
    public async Task PostDestacar_DeRascunho_Retorna409()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/destacar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostDestacar_DeAnuncioDeOutroVendedor_Retorna403()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(Guid.NewGuid());
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/destacar", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostAdicionarFotos_DoDonoComFotosValidas_Retorna200EAssociaAsFotos()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var mediaStorage = new FakeMediaStorage();
        using var factory = CriarFactory(repositorio, mediaStorage);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = new MultipartFormDataContent();
        AdicionarFotoFake(conteudo, "nova1.jpg");
        AdicionarFotoFake(conteudo, "nova2.jpg");

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/fotos", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Fotos.Count.ShouldBe(2);
        mediaStorage.ArquivosSalvos.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PostAdicionarFotos_ExcedendoOLimiteTotal_Retorna409SemAlterar()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        for (var i = 0; i < 9; i++)
            anuncio.AdicionarFoto($"/uploads/anuncios/existente{i}.jpg");
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = new MultipartFormDataContent();
        AdicionarFotoFake(conteudo, "nova1.jpg");
        AdicionarFotoFake(conteudo, "nova2.jpg");

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/fotos", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        anuncio.Fotos.Count.ShouldBe(9);
    }

    [Fact]
    public async Task PostAdicionarFotos_ComFotoDeTipoInvalido_Retorna400()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes("nao-e-imagem"));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        conteudo.Add(arquivo, "Fotos", "arquivo.txt");

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/fotos", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        anuncio.Fotos.ShouldBeEmpty();
    }

    [Fact]
    public async Task PostAdicionarFotos_DeAnuncioDeOutroVendedor_Retorna403()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var anuncio = CriarAnuncioDoVendedor(Guid.NewGuid());
        await repositorio.AdicionarAsync(anuncio);
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = new MultipartFormDataContent();
        AdicionarFotoFake(conteudo, "nova.jpg");

        var response = await client.PostAsync($"/api/anuncios/{anuncio.Id}/fotos", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostAdicionarFotos_ComIdInexistente_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var conteudo = new MultipartFormDataContent();
        AdicionarFotoFake(conteudo, "nova.jpg");

        var response = await client.PostAsync($"/api/anuncios/{Guid.NewGuid()}/fotos", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostAdicionarFotos_SemTokenAuthorization_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();
        var conteudo = new MultipartFormDataContent();
        AdicionarFotoFake(conteudo, "nova.jpg");

        var response = await client.PostAsync($"/api/anuncios/{Guid.NewGuid()}/fotos", conteudo, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteFoto_DoDonoComFotoExistente_Retorna200ERemoveDoStorage()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var mediaStorage = new FakeMediaStorage();
        using var factory = CriarFactory(repositorio, mediaStorage);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");
        mediaStorage.ArquivosSalvos.Add("/uploads/anuncios/foto1.jpg");
        await repositorio.AdicionarAsync(anuncio);
        var fotoId = anuncio.Fotos[0].Id;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/anuncios/{anuncio.Id}/fotos/{fotoId}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioResponse>(JsonOptions, ct);
        corpo!.Fotos.ShouldBeEmpty();
        mediaStorage.ArquivosSalvos.ShouldNotContain("/uploads/anuncios/foto1.jpg");
    }

    [Fact]
    public async Task DeleteFoto_ComIdDeFotoInexistente_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var (token, vendedorId) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        var anuncio = CriarAnuncioDoVendedor(vendedorId);
        await repositorio.AdicionarAsync(anuncio);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/anuncios/{anuncio.Id}/fotos/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteFoto_DeAnuncioDeOutroVendedor_Retorna403ENaoRemove()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();
        var anuncio = CriarAnuncioDoVendedor(Guid.NewGuid());
        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");
        await repositorio.AdicionarAsync(anuncio);
        var fotoId = anuncio.Fotos[0].Id;
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/anuncios/{anuncio.Id}/fotos/{fotoId}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        anuncio.Fotos.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DeleteFoto_ComIdDeAnuncioInexistente_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();
        var (token, _) = GerarTokenParaVendedor(TipoVendedor.PessoaFisica);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/anuncios/{Guid.NewGuid()}/fotos/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteFoto_SemTokenAuthorization_Retorna401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CriarFactory(new FakeAnuncioRepository());
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/anuncios/{Guid.NewGuid()}/fotos/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
