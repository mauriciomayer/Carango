using Carango.Api.Contracts;
using Carango.Application;
using Microsoft.AspNetCore.Mvc;

namespace Carango.Api.Controllers;

// Primeiro controller do projeto sem [Authorize] — busca é pública, o Comprador nunca
// precisa de conta (não existe nenhuma story de cadastro/login de Comprador em nenhum épico).
// Rota própria (api/busca), não uma extensão de AnunciosController (que é [Authorize] a
// nível de classe) — endpoint novo e explicitamente distinto do GET /api/anuncios owner-only
// da Story 2.5, conforme Action Item #2 da retrospectiva da Epic 2
[ApiController]
[Route("api/busca")]
public class BuscaController : ControllerBase
{
    private readonly BuscarAnunciosService _buscarAnunciosService;

    public BuscaController(BuscarAnunciosService buscarAnunciosService)
    {
        _buscarAnunciosService = buscarAnunciosService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AnuncioBuscaResponse>>> Buscar(
        [FromQuery] string? marca,
        [FromQuery] string? modelo,
        [FromQuery] int? ano,
        [FromQuery] string? versao,
        [FromQuery] decimal? precoMin,
        [FromQuery] decimal? precoMax,
        [FromQuery] string? estado,
        [FromQuery] string? cidade,
        [FromQuery] string? ordenarPor,
        [FromQuery] string? termo,
        [FromQuery] int? pagina)
    {
        // Trim antes de montar o filtro — achado no code review da Story 3.1: um valor com espaço
        // nas pontas (ex. copiado/colado) faz o Contains/igualdade da Infrastructure falhar
        // silenciosamente, devolvendo zero resultados sem nenhum sinal de que o problema é só espaço.
        // "termo" (não "q") — nome em português, consistente com os outros parâmetros deste
        // endpoint (AD-8), mesmo sendo diferente da convenção comum de motores de busca.
        // Argumentos nomeados — achado no code review da Story 3.3: com 11 campos, vários do
        // mesmo tipo (string?), a construção posicional ficava frágil a troca de ordem
        var filtro = new FiltroBusca(
            Marca: marca?.Trim(),
            Modelo: modelo?.Trim(),
            Ano: ano,
            Versao: versao?.Trim(),
            PrecoMin: precoMin,
            PrecoMax: precoMax,
            Estado: estado?.Trim(),
            Cidade: cidade?.Trim(),
            Ordenacao: ParaOrdenacao(ordenarPor),
            TermoLivre: termo?.Trim(),
            Pagina: pagina ?? 1);
        var anuncios = await _buscarAnunciosService.BuscarAsync(filtro);

        var resposta = anuncios.Select(a => new AnuncioBuscaResponse(
            a.Id, a.Marca, a.Modelo, a.Ano, a.Versao, a.Preco, a.Estado, a.Cidade,
            a.Fotos.OrderBy(f => f.Ordem).Select(f => f.Url).ToList(), a.Patrocinado)).ToList();

        return Ok(resposta);
    }

    // mesmo controller de Buscar() (lista) — mesma superfície pública de busca/descoberta, mesmo
    // padrão de AnunciosController já ter Listar()/Obter(id) lado a lado (Story 2.5). Sem
    // [Authorize], sem conceito de posse: qualquer id de Anúncio Ativo é visível pra qualquer um
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnuncioDetalheResponse>> Obter(Guid id)
    {
        try
        {
            var anuncio = await _buscarAnunciosService.ObterDetalhePublicoAsync(id);
            // ThenBy(Id) como desempate — achado no code review: duas Fotos com o mesmo Ordem
            // não tinham ordem relativa definida; agora que a galeria do Detalhe (Story 3.5)
            // deixa a ordem das fotos navegável/visível, um empate real passa a importar mais
            // do que importava quando só a primeira foto era exibida (listagens/thumbnails)
            var resposta = new AnuncioDetalheResponse(
                anuncio.Id, anuncio.Marca, anuncio.Modelo, anuncio.Ano, anuncio.Versao,
                anuncio.Preco, anuncio.Descricao, anuncio.Estado, anuncio.Cidade,
                anuncio.Fotos.OrderBy(f => f.Ordem).ThenBy(f => f.Id).Select(f => f.Url).ToList());
            return Ok(resposta);
        }
        catch (AnuncioNaoEncontradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Anúncio não encontrado");
        }
    }

    // valor ausente ou não reconhecível → Relevancia, nunca erro — mesma filosofia de degradação
    // graciosa já usada pra combinações de filtro sem sentido na Story 3.1. Nomes em kebab-case
    // na query string (AD-8, contrato em português), não os nomes do enum C# diretamente.
    // Trim + lower antes de comparar — achado no code review: sem isso, "ordenarPor" ficava
    // inconsistente com os outros parâmetros deste mesmo endpoint (que já são trimados/case-insensitive)
    private static OrdenacaoBusca ParaOrdenacao(string? ordenarPor) => ordenarPor?.Trim().ToLowerInvariant() switch
    {
        "preco-asc" => OrdenacaoBusca.PrecoAsc,
        "preco-desc" => OrdenacaoBusca.PrecoDesc,
        "ano-asc" => OrdenacaoBusca.AnoAsc,
        "ano-desc" => OrdenacaoBusca.AnoDesc,
        _ => OrdenacaoBusca.Relevancia,
    };
}
