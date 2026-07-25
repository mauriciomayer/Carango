using System.Net;
using Carango.Application;
using Carango.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Carango.Tests.Infrastructure;

// achado no code review da Story 2.6: nenhum teste exercitava FipeVeiculoReferenciaGateway de
// verdade (todos os testes de Application/Api usam FakeVeiculoReferenciaGateway) — um bug real
// de construção de URL (BaseAddress sem barra final) só foi encontrado por smoke test manual.
// HttpMessageHandler substituído (sem rede real) prova a construção de URL/mapeamento de DTO
// sem depender da Fipe estar de pé nem gastar a cota gratuita
public class FipeVeiculoReferenciaGatewayTests
{
    // stub simples que grava a última requisição e devolve a resposta configurada — mesmo
    // espírito de FakeXxxRepository (testar comportamento, não implementação)
    private class HttpMessageHandlerStub : HttpMessageHandler
    {
        public HttpRequestMessage? UltimaRequisicao { get; private set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string Corpo { get; set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaRequisicao = request;
            var resposta = new HttpResponseMessage(StatusCode) { Content = new StringContent(Corpo) };
            return Task.FromResult(resposta);
        }
    }

    private static (FipeVeiculoReferenciaGateway Gateway, HttpMessageHandlerStub Handler) CriarGateway()
    {
        var handler = new HttpMessageHandlerStub();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://parallelum.com.br/fipe/api/v1/") };
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new FipeVeiculoReferenciaGateway(httpClient, cache), handler);
    }

    [Fact]
    public async Task ListarMarcasAsync_Sempre_ChamaOCaminhoCorreto()
    {
        var (gateway, handler) = CriarGateway();
        handler.Corpo = """[{"codigo":"59","nome":"VW - VolksWagen"}]""";

        var resultado = await gateway.ListarMarcasAsync();

        // prova o bug real encontrado no smoke test: sem a barra final no BaseAddress + sem
        // barra inicial no caminho relativo, o .NET monta a URL errada (troca o path do
        // BaseAddress em vez de anexar) — este teste falharia com a versão com bug
        handler.UltimaRequisicao!.RequestUri!.ToString().ShouldBe("https://parallelum.com.br/fipe/api/v1/carros/marcas");
        resultado.ShouldHaveSingleItem();
        resultado[0].Codigo.ShouldBe("59");
        resultado[0].Nome.ShouldBe("VW - VolksWagen");
    }

    [Fact]
    public async Task ListarModelosAsync_Sempre_ChamaOCaminhoComACodigoDaMarcaESoRetornaOsModelos()
    {
        var (gateway, handler) = CriarGateway();
        // resposta real da Fipe também tem "anos" — só "modelos" deve ser mapeado
        handler.Corpo = """{"modelos":[{"codigo":5585,"nome":"AMAROK"}],"anos":[{"codigo":"2019-1","nome":"2019 Gasolina"}]}""";

        var resultado = await gateway.ListarModelosAsync("59");

        handler.UltimaRequisicao!.RequestUri!.ToString().ShouldBe("https://parallelum.com.br/fipe/api/v1/carros/marcas/59/modelos");
        resultado.ShouldHaveSingleItem();
        resultado[0].Codigo.ShouldBe("5585");
        resultado[0].Nome.ShouldBe("AMAROK");
    }

    [Fact]
    public async Task ListarMarcasAsync_ComStatusDeErro_LancaVeiculoReferenciaIndisponivel()
    {
        var (gateway, handler) = CriarGateway();
        handler.StatusCode = HttpStatusCode.NotFound;

        await Should.ThrowAsync<VeiculoReferenciaIndisponivelException>(() => gateway.ListarMarcasAsync());
    }

    [Fact]
    public async Task ListarModelosAsync_ComRespostaSemCampoModelos_LancaVeiculoReferenciaIndisponivelEmVezDeExcecaoNaoTratada()
    {
        // achado no code review: uma resposta 200 sem o campo "modelos" (marca inexistente/
        // formato inesperado da Fipe) deserializa Modelos como null — sem o catch amplo, o
        // .Select() subsequente lançaria NullReferenceException não capturada (500)
        var (gateway, handler) = CriarGateway();
        handler.Corpo = """{"outraCoisa":true}""";

        await Should.ThrowAsync<VeiculoReferenciaIndisponivelException>(() => gateway.ListarModelosAsync("59"));
    }

    [Fact]
    public async Task ListarMarcasAsync_ComCorpoJsonInvalido_LancaVeiculoReferenciaIndisponivel()
    {
        var (gateway, handler) = CriarGateway();
        handler.Corpo = "isto não é json";

        await Should.ThrowAsync<VeiculoReferenciaIndisponivelException>(() => gateway.ListarMarcasAsync());
    }

    // achado em teste manual do usuário: reproduzido direto contra a Fipe real (curl no mesmo
    // instante confirmou a API respondendo 200 enquanto o nosso endpoint devolvia 503) — uma
    // falha passageira ficava "envenenada" em cache por 24h (TtlCache), porque
    // IMemoryCacheExtensions.GetOrCreateAsync comita o ICacheEntry mesmo quando a factory lança.
    // Este teste prova que uma falha NÃO fica em cache: uma tentativa que funciona depois de uma
    // que falhou tem que ter sucesso, não repetir a mesma exceção
    [Fact]
    public async Task ListarModelosAsync_ApósUmaFalhaPassageira_UmaNovaTentativaConsultaAFipeDeNovoEFunciona()
    {
        var (gateway, handler) = CriarGateway();
        handler.StatusCode = HttpStatusCode.NotFound;

        await Should.ThrowAsync<VeiculoReferenciaIndisponivelException>(() => gateway.ListarModelosAsync("21"));

        handler.StatusCode = HttpStatusCode.OK;
        handler.Corpo = """{"modelos":[{"codigo":437,"nome":"147 C/ CL"}]}""";

        var resultado = await gateway.ListarModelosAsync("21");

        resultado.ShouldHaveSingleItem();
        resultado[0].Nome.ShouldBe("147 C/ CL");
    }

    [Fact]
    public async Task ListarMarcasAsync_ApósUmaFalhaPassageira_UmaNovaTentativaConsultaAFipeDeNovoEFunciona()
    {
        var (gateway, handler) = CriarGateway();
        handler.StatusCode = HttpStatusCode.NotFound;

        await Should.ThrowAsync<VeiculoReferenciaIndisponivelException>(() => gateway.ListarMarcasAsync());

        handler.StatusCode = HttpStatusCode.OK;
        handler.Corpo = """[{"codigo":"59","nome":"VW - VolksWagen"}]""";

        var resultado = await gateway.ListarMarcasAsync();

        resultado.ShouldHaveSingleItem();
        resultado[0].Nome.ShouldBe("VW - VolksWagen");
    }

    // achado no code review — não basta uma tentativa depois de falha funcionar; um sucesso
    // subsequente também precisa vir do cache (não gastar 2 requisições HTTP pra 2 chamadas
    // idênticas), senão o Set manual introduzido pelo fix poderia ter substituído o cache real
    // por um no-op disfarçado
    [Fact]
    public async Task ListarModelosAsync_ChamadoDuasVezesComSucesso_SoConsultaAFipeUmaVez()
    {
        var (gateway, handler) = CriarGateway();
        handler.Corpo = """{"modelos":[{"codigo":5585,"nome":"AMAROK"}]}""";

        await gateway.ListarModelosAsync("59");
        handler.Corpo = """{"modelos":[{"codigo":9999,"nome":"OUTRO"}]}""";
        var segundaChamada = await gateway.ListarModelosAsync("59");

        // se a segunda chamada tivesse ido à rede de novo, viria "OUTRO" (o corpo mudou) — vindo
        // do cache, continua "AMAROK" (o valor da primeira chamada, nunca re-consultado)
        segundaChamada[0].Nome.ShouldBe("AMAROK");
    }
}
