using System.Net;
using System.Net.Http.Json;
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

public class BuscaControllerTests
{
    private static WebApplicationFactory<Program> CriarFactory(FakeAnuncioRepository repositorio)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            JwtConfiguracaoTeste.Aplicar(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnuncioRepository>();
                services.AddSingleton<IAnuncioRepository>(repositorio);

                services.RemoveAll<IMediaStorage>();
                services.AddSingleton<IMediaStorage>(new FakeMediaStorage());
            });
        });
    }

    private static Anuncio CriarAtivo(
        string marca = "Honda", string modelo = "Civic", int ano = 2019, string versao = "EXL",
        decimal preco = 95000m, string estado = "SP", string cidade = "São Paulo", string descricao = "Descrição qualquer")
    {
        var anuncio = Anuncio.CriarRascunho(
            Guid.NewGuid(), marca, modelo, ano, versao, preco, descricao, estado, cidade);
        anuncio.Publicar();
        return anuncio;
    }

    [Fact]
    public async Task GetBusca_SemAuthorizationHeader_Retorna200()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo());
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBusca_SemFiltro_RetornaSoAnunciosAtivos()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var ativo = CriarAtivo();
        var rascunho = Anuncio.CriarRascunho(Guid.NewGuid(), marca: "Fiat", modelo: "Uno");
        var pausado = CriarAtivo(marca: "Ford", modelo: "Ka");
        pausado.Pausar();
        await repositorio.AdicionarAsync(ativo);
        await repositorio.AdicionarAsync(rascunho);
        await repositorio.AdicionarAsync(pausado);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(ativo.Id);
    }

    [Fact]
    public async Task GetBusca_ComMarcaEEstadoCombinados_RetornaSoOsQueAtendemAosDois()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combinaTudo = CriarAtivo(marca: "Honda", estado: "SP");
        var combinaSoMarca = CriarAtivo(marca: "Honda", estado: "RJ");
        await repositorio.AdicionarAsync(combinaTudo);
        await repositorio.AdicionarAsync(combinaSoMarca);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?marca=Honda&estado=SP", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combinaTudo.Id);
    }

    [Fact]
    public async Task GetBusca_SemNenhumResultado_Retorna200ComListaVazia()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(marca: "Honda"));
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?marca=Ferrari", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBusca_ComModeloFiltro_RetornaSoOsQueCombinam()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(modelo: "Civic");
        var naoCombina = CriarAtivo(modelo: "Corolla");
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?modelo=Civic", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combina.Id);
    }

    [Fact]
    public async Task GetBusca_ComCidadeFiltro_RetornaSoOsQueCombinam()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(cidade: "Curitiba");
        var naoCombina = CriarAtivo(cidade: "Recife");
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?cidade=Curitiba", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combina.Id);
    }

    [Fact]
    public async Task GetBusca_ComAnoFiltro_RetornaSoOsQueCombinam()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(ano: 2020);
        var naoCombina = CriarAtivo(ano: 2015);
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ano=2020", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combina.Id);
    }

    [Fact]
    public async Task GetBusca_ComVersaoFiltro_RetornaSoOsQueCombinam()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(versao: "EXL");
        var naoCombina = CriarAtivo(versao: "LX");
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?versao=EXL", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combina.Id);
    }

    [Fact]
    public async Task GetBusca_ComMarcaComEspacosNasPontas_IgnoraOsEspacos()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(marca: "Honda");
        await repositorio.AdicionarAsync(combina);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?marca=%20Honda%20", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combina.Id);
    }

    [Fact]
    public async Task GetBusca_ComFaixaDePreco_IncluiCorretamenteNosLimites()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var noLimiteMin = CriarAtivo(preco: 50000m);
        var noLimiteMax = CriarAtivo(preco: 70000m);
        var foraDoLimite = CriarAtivo(preco: 70000.01m);
        await repositorio.AdicionarAsync(noLimiteMin);
        await repositorio.AdicionarAsync(noLimiteMax);
        await repositorio.AdicionarAsync(foraDoLimite);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?precoMin=50000&precoMax=70000", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Count.ShouldBe(2);
        corpo.Select(a => a.Id).ShouldContain(noLimiteMin.Id);
        corpo.Select(a => a.Id).ShouldContain(noLimiteMax.Id);
    }

    [Fact]
    public async Task GetBusca_ComOrdenarPorPrecoAsc_RetornaOrdenadoDoMaisBaratoAoMaisCaro()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(preco: 90000m));
        await repositorio.AdicionarAsync(CriarAtivo(preco: 40000m));
        await repositorio.AdicionarAsync(CriarAtivo(preco: 60000m));
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=preco-asc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Select(a => a.Preco).ShouldBe([40000m, 60000m, 90000m]);
    }

    [Fact]
    public async Task GetBusca_ComOrdenarPorPrecoDesc_RetornaOrdenadoDoMaisCaroAoMaisBarato()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(preco: 90000m));
        await repositorio.AdicionarAsync(CriarAtivo(preco: 40000m));
        await repositorio.AdicionarAsync(CriarAtivo(preco: 60000m));
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=preco-desc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Select(a => a.Preco).ShouldBe([90000m, 60000m, 40000m]);
    }

    [Fact]
    public async Task GetBusca_ComOrdenarPorAnoAsc_RetornaOrdenadoDoMaisAntigoAoMaisNovo()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(ano: 2022));
        await repositorio.AdicionarAsync(CriarAtivo(ano: 2015));
        await repositorio.AdicionarAsync(CriarAtivo(ano: 2019));
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=ano-asc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Select(a => a.Ano).ShouldBe([2015, 2019, 2022]);
    }

    [Fact]
    public async Task GetBusca_ComOrdenarPorAnoDesc_RetornaOrdenadoDoMaisNovoAoMaisAntigo()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(ano: 2022));
        await repositorio.AdicionarAsync(CriarAtivo(ano: 2015));
        await repositorio.AdicionarAsync(CriarAtivo(ano: 2019));
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=ano-desc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Select(a => a.Ano).ShouldBe([2022, 2019, 2015]);
    }

    [Fact]
    public async Task GetBusca_SemOrdenarPorOuComValorDesconhecido_ContinuaOrdenandoPorCriadoEmDecrescente()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var primeiro = CriarAtivo(marca: "Honda");
        await repositorio.AdicionarAsync(primeiro);
        await Task.Delay(20, ct);
        var segundo = CriarAtivo(marca: "Toyota");
        await repositorio.AdicionarAsync(segundo);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var respostaSemParametro = await client.GetAsync("/api/busca", ct);
        var respostaValorDesconhecido = await client.GetAsync("/api/busca?ordenarPor=xyz", ct);

        respostaValorDesconhecido.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpoSemParametro = await respostaSemParametro.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        var corpoValorDesconhecido = await respostaValorDesconhecido.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpoSemParametro!.Select(a => a.Id).ShouldBe([segundo.Id, primeiro.Id]);
        corpoValorDesconhecido!.Select(a => a.Id).ShouldBe([segundo.Id, primeiro.Id]);
    }

    [Fact]
    public async Task GetBusca_ComFiltroDeCampoEOrdenacaoCombinados_AplicaAsDuasCoisas()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var hondaCaro = CriarAtivo(marca: "Honda", preco: 90000m);
        var hondaBarato = CriarAtivo(marca: "Honda", preco: 40000m);
        var toyota = CriarAtivo(marca: "Toyota", preco: 30000m);
        await repositorio.AdicionarAsync(hondaCaro);
        await repositorio.AdicionarAsync(hondaBarato);
        await repositorio.AdicionarAsync(toyota);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?marca=Honda&ordenarPor=preco-asc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Select(a => a.Id).ShouldBe([hondaBarato.Id, hondaCaro.Id]);
    }

    [Fact]
    public async Task GetBusca_ComOrdenarPorComEspacosEMaiusculas_ContinuaReconhecendoOValor()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(preco: 90000m));
        await repositorio.AdicionarAsync(CriarAtivo(preco: 40000m));
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=%20Preco-Asc%20", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Select(a => a.Preco).ShouldBe([40000m, 90000m]);
    }

    [Fact]
    public async Task GetBusca_ComFaixaDePrecoEOrdenarPorAno_AplicaAsDuasCoisas()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var noAlvoNovo = CriarAtivo(preco: 60000m, ano: 2022);
        var noAlvoAntigo = CriarAtivo(preco: 55000m, ano: 2015);
        var foraDoAlvo = CriarAtivo(preco: 90000m, ano: 2020);
        await repositorio.AdicionarAsync(noAlvoNovo);
        await repositorio.AdicionarAsync(noAlvoAntigo);
        await repositorio.AdicionarAsync(foraDoAlvo);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?precoMin=50000&precoMax=70000&ordenarPor=ano-desc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Select(a => a.Id).ShouldBe([noAlvoNovo.Id, noAlvoAntigo.Id]);
    }

    [Fact]
    public async Task GetBusca_ComTermoQueCombina_RetornaSoOsQueCombinam()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(modelo: "Civic");
        var naoCombina = CriarAtivo(modelo: "Corolla");
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?termo=Civic", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combina.Id);
    }

    [Fact]
    public async Task GetBusca_ComTermoDeDuasPalavras_ExigeAsDuasEmQualquerCampo()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combinaTudo = CriarAtivo(modelo: "Civic", descricao: "Modelo 2019, único dono");
        var combinaSoModelo = CriarAtivo(modelo: "Civic", descricao: "Carro de garagem");
        await repositorio.AdicionarAsync(combinaTudo);
        await repositorio.AdicionarAsync(combinaSoModelo);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?termo=Civic+2019", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combinaTudo.Id);
    }

    [Fact]
    public async Task GetBusca_ComTermoEFiltroEOrdenacaoCombinados_AplicaTudoJunto()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combinaTudo = CriarAtivo(modelo: "Civic", estado: "SP", preco: 60000m);
        var combinaSoTermo = CriarAtivo(modelo: "Civic", estado: "RJ", preco: 50000m);
        await repositorio.AdicionarAsync(combinaTudo);
        await repositorio.AdicionarAsync(combinaSoTermo);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?termo=Civic&estado=SP&ordenarPor=preco-asc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combinaTudo.Id);
    }

    [Fact]
    public async Task GetBusca_ComTermoSemNenhumResultado_Retorna200ComListaVazia()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(modelo: "Civic"));
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?termo=Ferrari", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBusca_ComTermoComEspacosNasPontas_IgnoraOsEspacos()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(modelo: "Civic");
        await repositorio.AdicionarAsync(combina);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?termo=%20Civic%20", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(combina.Id);
    }

    [Fact]
    public async Task GetBusca_ComTermoSoDeEspacos_ComportaComoSemTermoNenhum()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var ativo = CriarAtivo(modelo: "Civic");
        await repositorio.AdicionarAsync(ativo);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?termo=%20%20%20", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldHaveSingleItem();
        corpo[0].Id.ShouldBe(ativo.Id);
    }

    // 25 Anúncios com preço ascendente e distinto (10000, 10100, ..., 12400), ordenados por
    // preco-asc — dá uma ordenação 100% determinística pros testes de paginação, sem depender
    // de CriadoEm (timing real, mesmo "test smell" já registrado no deferred-work da Story 2.5)
    private static async Task<List<Anuncio>> Criar25AnunciosComPrecoAscendente(FakeAnuncioRepository repositorio)
    {
        var anuncios = new List<Anuncio>();
        for (var i = 0; i < 25; i++)
        {
            var anuncio = CriarAtivo(preco: 10000m + i * 100m);
            anuncios.Add(anuncio);
            await repositorio.AdicionarAsync(anuncio);
        }
        return anuncios;
    }

    [Fact]
    public async Task GetBusca_ComMaisDe20Resultados_Pagina1RetornaOsPrimeiros20()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var anuncios = await Criar25AnunciosComPrecoAscendente(repositorio);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=preco-asc&pagina=1", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Count.ShouldBe(20);
        corpo.Select(a => a.Id).ShouldBe(anuncios.Take(20).Select(a => a.Id));
    }

    [Fact]
    public async Task GetBusca_ComMaisDe20Resultados_SemPaginaRetornaAPrimeiraPagina()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var anuncios = await Criar25AnunciosComPrecoAscendente(repositorio);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=preco-asc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Count.ShouldBe(20);
        corpo.Select(a => a.Id).ShouldBe(anuncios.Take(20).Select(a => a.Id));
    }

    [Fact]
    public async Task GetBusca_ComMaisDe20Resultados_Pagina2RetornaOsRestantes()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var anuncios = await Criar25AnunciosComPrecoAscendente(repositorio);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=preco-asc&pagina=2", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Count.ShouldBe(5);
        corpo.Select(a => a.Id).ShouldBe(anuncios.Skip(20).Take(5).Select(a => a.Id));
    }

    [Fact]
    public async Task GetBusca_ComPaginaAlemDoFim_RetornaListaVazia()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await Criar25AnunciosComPrecoAscendente(repositorio);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=preco-asc&pagina=3", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBusca_ComPaginaZeroOuNegativa_ComportaComoPagina1()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var anuncios = await Criar25AnunciosComPrecoAscendente(repositorio);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var respostaZero = await client.GetAsync("/api/busca?ordenarPor=preco-asc&pagina=0", ct);
        var respostaNegativa = await client.GetAsync("/api/busca?ordenarPor=preco-asc&pagina=-5", ct);

        var corpoZero = await respostaZero.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        var corpoNegativa = await respostaNegativa.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpoZero!.Select(a => a.Id).ShouldBe(anuncios.Take(20).Select(a => a.Id));
        corpoNegativa!.Select(a => a.Id).ShouldBe(anuncios.Take(20).Select(a => a.Id));
    }

    [Fact]
    public async Task GetBusca_ComFiltroEOrdenacaoNaPagina2_MantemFiltroEOrdenacaoCorretos()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var comMarca = new List<Anuncio>();
        for (var i = 0; i < 22; i++)
        {
            var anuncio = CriarAtivo(marca: "Honda", preco: 10000m + i * 100m);
            comMarca.Add(anuncio);
            await repositorio.AdicionarAsync(anuncio);
        }
        await repositorio.AdicionarAsync(CriarAtivo(marca: "Toyota", preco: 5000m));
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?marca=Honda&ordenarPor=preco-asc&pagina=2", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Count.ShouldBe(2);
        corpo.Select(a => a.Id).ShouldBe(comMarca.Skip(20).Take(2).Select(a => a.Id));
    }

    [Fact]
    public async Task GetBusca_ComPaginaMuitoGrande_NaoEstouraENaoDaErro()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        await Criar25AnunciosComPrecoAscendente(repositorio);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/busca?ordenarPor=preco-asc&pagina={int.MaxValue}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBuscaId_SemAuthorizationHeader_Retorna200ComTodosOsCampos()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var anuncio = CriarAtivo(descricao: "Único dono, revisado na concessionária");
        await repositorio.AdicionarAsync(anuncio);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/busca/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<AnuncioDetalheResponse>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo!.Id.ShouldBe(anuncio.Id);
        corpo.Marca.ShouldBe(anuncio.Marca);
        corpo.Modelo.ShouldBe(anuncio.Modelo);
        corpo.Ano.ShouldBe(anuncio.Ano);
        corpo.Versao.ShouldBe(anuncio.Versao);
        corpo.Preco.ShouldBe(anuncio.Preco);
        corpo.Descricao.ShouldBe(anuncio.Descricao);
        corpo.Estado.ShouldBe(anuncio.Estado);
        corpo.Cidade.ShouldBe(anuncio.Cidade);
        corpo.Fotos.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBuscaId_ComAnuncioComVisualizacoes_NaoVazaOCampoNoContratoPublico()
    {
        // achado no code review (Blind Hunter): Visualizacoes é verdade "por omissão" no contrato
        // público (AnuncioDetalheResponse não declara o campo) — sem este teste, nada pega um futuro
        // dev que adicione o campo de volta "pra manter paridade" com o contrato owner-facing
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var anuncio = CriarAtivo();
        await repositorio.AdicionarAsync(anuncio);
        await repositorio.IncrementarVisualizacaoAsync(anuncio.Id);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/busca/{anuncio.Id}", ct);

        var corpoBruto = await response.Content.ReadAsStringAsync(ct);
        corpoBruto.ShouldNotContain("isualizaco", Case.Insensitive);
    }

    [Fact]
    public async Task GetBusca_ComAnuncioComVisualizacoes_NaoVazaOCampoNoContratoPublico()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var anuncio = CriarAtivo();
        await repositorio.AdicionarAsync(anuncio);
        await repositorio.IncrementarVisualizacaoAsync(anuncio.Id);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca", ct);

        var corpoBruto = await response.Content.ReadAsStringAsync(ct);
        corpoBruto.ShouldNotContain("isualizaco", Case.Insensitive);
    }

    [Fact]
    public async Task GetBuscaId_ComIdInexistente_Retorna404()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/busca/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("rascunho")]
    [InlineData("pausado")]
    [InlineData("vendido")]
    public async Task GetBuscaId_ComAnuncioNaoAtivo_Retorna404(string status)
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var anuncio = Anuncio.CriarRascunho(
            Guid.NewGuid(), "Honda", "Civic", 2019, "EXL", 95000m, "Descrição qualquer", "SP", "São Paulo");
        if (status != "rascunho")
        {
            anuncio.Publicar();
            if (status == "pausado") anuncio.Pausar();
            if (status == "vendido") anuncio.MarcarComoVendido();
        }
        await repositorio.AdicionarAsync(anuncio);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/busca/{anuncio.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBusca_ComOrdenacaoRelevancia_PriorizaAnuncioPatrocinado()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        // patrocinado é criado PRIMEIRO (CriadoEm mais antigo) e naoPatrocinado depois (mais
        // recente) — de propósito invertido: o desempate por CriadoEm sozinho colocaria o mais
        // recente primeiro, então só um Patrocinado get anteposto ao mais recente prova que é o
        // Patrocinado (e não a recência) que está determinando a ordem (AC #3 exige "mesmo sendo
        // mais antigo"; achado no code review — a versão anterior deste teste criava o Patrocinado
        // por último, então passava mesmo que Patrocinado não influenciasse a ordenação em nada)
        var patrocinado = CriarAtivo(preco: 90000m);
        patrocinado.Destacar();
        await repositorio.AdicionarAsync(patrocinado);
        Thread.Sleep(20);
        var naoPatrocinado = CriarAtivo(preco: 40000m);
        await repositorio.AdicionarAsync(naoPatrocinado);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo![0].Id.ShouldBe(patrocinado.Id);
        corpo[0].Patrocinado.ShouldBeTrue();
        corpo[1].Id.ShouldBe(naoPatrocinado.Id);
        corpo[1].Patrocinado.ShouldBeFalse();
    }

    [Fact]
    public async Task GetBusca_ComOrdenacaoExplicitaPorPreco_NaoPriorizaAnuncioPatrocinado()
    {
        var ct = TestContext.Current.CancellationToken;
        var repositorio = new FakeAnuncioRepository();
        var barato = CriarAtivo(preco: 40000m);
        await repositorio.AdicionarAsync(barato);
        var patrocinadoCaro = CriarAtivo(preco: 90000m);
        patrocinadoCaro.Destacar();
        await repositorio.AdicionarAsync(patrocinadoCaro);
        using var factory = CriarFactory(repositorio);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/busca?ordenarPor=preco-asc", ct);

        var corpo = await response.Content.ReadFromJsonAsync<List<AnuncioBuscaResponse>>(cancellationToken: ct);
        corpo.ShouldNotBeNull();
        corpo![0].Id.ShouldBe(barato.Id);
        corpo[1].Id.ShouldBe(patrocinadoCaro.Id);
    }
}
