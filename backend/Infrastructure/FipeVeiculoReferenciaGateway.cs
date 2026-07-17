using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carango.Application;
using Microsoft.Extensions.Caching.Memory;

namespace Carango.Infrastructure;

// única implementação de IVeiculoReferenciaGateway (AD-12) — fala com a API pública da Fipe
// (https://parallelum.com.br/fipe/api/v1, sem autenticação obrigatória, confirmada funcionando
// em 2026-07-14). HttpClient injetado via AddHttpClient<IVeiculoReferenciaGateway,
// FipeVeiculoReferenciaGateway> (BaseAddress/Timeout configurados em
// InfrastructureServiceCollectionExtensions), não construído aqui
public class FipeVeiculoReferenciaGateway : IVeiculoReferenciaGateway
{
    // 24h — a tabela Fipe é atualizada mensalmente pela própria Fipe, então é conservador o
    // bastante pra nunca mostrar dado desatualizado de forma perceptível, e protege o limite de
    // 500 req/dia sem token contra qualquer tráfego real (decisão deixada em aberto por
    // ARCHITECTURE-SPINE.md § AD-12, resolvida aqui — ver Dev Notes da Story 2.6)
    private static readonly TimeSpan TtlCache = TimeSpan.FromHours(24);
    private const string ChaveCacheMarcas = "fipe:marcas";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public FipeVeiculoReferenciaGateway(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public Task<IReadOnlyList<VeiculoReferenciaItem>> ListarMarcasAsync() =>
        _cache.GetOrCreateAsync(ChaveCacheMarcas, async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = TtlCache;
            var marcas = await BuscarAsync<List<FipeMarcaDto>>("carros/marcas");
            IReadOnlyList<VeiculoReferenciaItem> resultado = marcas
                .Select(m => new VeiculoReferenciaItem(m.Codigo, m.Nome))
                .ToList();
            return resultado;
        })!;

    public Task<IReadOnlyList<VeiculoReferenciaItem>> ListarModelosAsync(string marcaCodigo) =>
        _cache.GetOrCreateAsync($"fipe:modelos:{marcaCodigo}", async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = TtlCache;
            var resposta = await BuscarAsync<FipeModelosResponseDto>($"carros/marcas/{marcaCodigo}/modelos");
            // achado no code review: uma resposta 200 sem o campo "modelos" (formato inesperado
            // da Fipe) desserializa Modelos como null — sem esta checagem, o .Select() abaixo
            // lançaria ArgumentNullException fora do try/catch de BuscarAsync, escapando como
            // 500 não controlado em vez do 503 esperado
            if (resposta.Modelos is null) throw new VeiculoReferenciaIndisponivelException();
            IReadOnlyList<VeiculoReferenciaItem> resultado = resposta.Modelos
                .Select(m => new VeiculoReferenciaItem(m.Codigo.ToString(), m.Nome))
                .ToList();
            return resultado;
        })!;

    // qualquer falha ao falar com a Fipe (rede, timeout, status de erro, corpo malformado/
    // inesperado) vira VeiculoReferenciaIndisponivelException — a exceção original nunca vaza
    // pra fora do Infrastructure. Catch amplo de propósito (achado no code review: o filtro
    // anterior só cobria 3 tipos específicos, deixando escapar como 500 não controlado qualquer
    // outra falha, ex.: UriFormatException de um caminho malformado ou NullReferenceException
    // de uma resposta 200 sem o campo esperado)
    private async Task<T> BuscarAsync<T>(string caminho)
    {
        try
        {
            var resposta = await _httpClient.GetAsync(caminho);
            resposta.EnsureSuccessStatusCode();
            var corpo = await resposta.Content.ReadFromJsonAsync<T>(JsonOptions);
            return corpo ?? throw new VeiculoReferenciaIndisponivelException();
        }
        catch (VeiculoReferenciaIndisponivelException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new VeiculoReferenciaIndisponivelException();
        }
    }

    private record FipeMarcaDto(string Codigo, string Nome);

    private record FipeModelosResponseDto([property: JsonPropertyName("modelos")] List<FipeModeloDto> Modelos);

    private record FipeModeloDto(int Codigo, string Nome);
}
