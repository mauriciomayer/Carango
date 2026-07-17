using System.Security.Claims;
using Carango.Api.Contracts;
using Carango.Application;
using Carango.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carango.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/anuncios")]
public class AnunciosController : ControllerBase
{
    private const int MaxFotos = 10;
    private const long MaxTamanhoFotoBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> ExtensoesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly HashSet<string> TiposConteudoPermitidos =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private readonly CriarAnuncioService _criarAnuncioService;
    private readonly GerenciarAnuncioService _gerenciarAnuncioService;

    public AnunciosController(CriarAnuncioService criarAnuncioService, GerenciarAnuncioService gerenciarAnuncioService)
    {
        _criarAnuncioService = criarAnuncioService;
        _gerenciarAnuncioService = gerenciarAnuncioService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AnuncioResponse>>> Listar()
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        var anuncios = await _gerenciarAnuncioService.ListarAsync(vendedorId);
        return Ok(anuncios.Select(ParaResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnuncioResponse>> Obter(Guid id)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        try
        {
            var anuncio = await _gerenciarAnuncioService.ObterParaEdicaoAsync(id, vendedorId);
            return Ok(ParaResponse(anuncio));
        }
        catch (AnuncioNaoEncontradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Anúncio não encontrado");
        }
        catch (AnuncioNaoPertenceAoVendedorException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Acesso negado");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AnuncioResponse>> Editar(Guid id, [FromBody] EditarAnuncioRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        try
        {
            var input = new EditarAnuncioInput(
                id, vendedorId, request.Marca, request.Modelo, request.Ano, request.Versao,
                request.Preco, request.Descricao, request.Estado, request.Cidade);

            var anuncio = await _gerenciarAnuncioService.EditarAsync(input);
            return Ok(ParaResponse(anuncio));
        }
        catch (AnuncioNaoEncontradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Anúncio não encontrado");
        }
        catch (AnuncioNaoPertenceAoVendedorException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Acesso negado");
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        try
        {
            await _gerenciarAnuncioService.ExcluirAsync(id, vendedorId);
            return NoContent();
        }
        catch (AnuncioNaoEncontradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Anúncio não encontrado");
        }
        catch (AnuncioNaoPertenceAoVendedorException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Acesso negado");
        }
    }

    [HttpPost("{id:guid}/pausar")]
    public Task<ActionResult<AnuncioResponse>> Pausar(Guid id) =>
        ExecutarTransicaoAsync(vendedorId => _gerenciarAnuncioService.PausarAsync(id, vendedorId));

    [HttpPost("{id:guid}/reativar")]
    public Task<ActionResult<AnuncioResponse>> Reativar(Guid id) =>
        ExecutarTransicaoAsync(vendedorId => _gerenciarAnuncioService.ReativarAsync(id, vendedorId));

    [HttpPost("{id:guid}/marcar-vendido")]
    public Task<ActionResult<AnuncioResponse>> MarcarVendido(Guid id) =>
        ExecutarTransicaoAsync(vendedorId => _gerenciarAnuncioService.MarcarComoVendidoAsync(id, vendedorId));

    [HttpPost("{id:guid}/destacar")]
    public Task<ActionResult<AnuncioResponse>> Destacar(Guid id) =>
        ExecutarTransicaoAsync(vendedorId => _gerenciarAnuncioService.DestacarAsync(id, vendedorId));

    // as transições de status (Pausar/Reativar/MarcarVendido/Destacar) não têm corpo de requisição e
    // compartilham a maior parte do mesmo conjunto de exceções — extraído aqui pra não repetir o mesmo
    // bloco try/catch (GET/PUT continuam com seus próprios blocos, conjunto de exceções diferente).
    // CobrancaFalhouException (Story 4.1) só é lançada por Destacar — inofensivo as outras 3 transições
    // nunca lançarem essa exceção específica, o catch simplesmente nunca é alcançado por elas
    private async Task<ActionResult<AnuncioResponse>> ExecutarTransicaoAsync(Func<Guid, Task<Anuncio>> transicao)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        try
        {
            var anuncio = await transicao(vendedorId);
            return Ok(ParaResponse(anuncio));
        }
        catch (AnuncioNaoEncontradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Anúncio não encontrado");
        }
        catch (AnuncioNaoPertenceAoVendedorException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Acesso negado");
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Transição de status inválida");
        }
        catch (LimiteDeAnunciosAtivosExcedidoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Limite de Anúncios ativos excedido");
        }
        catch (CobrancaFalhouException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Pagamento não concluído");
        }
    }

    private static AnuncioResponse ParaResponse(Anuncio anuncio) => new(
        anuncio.Id, anuncio.VendedorId, anuncio.Marca, anuncio.Modelo, anuncio.Ano, anuncio.Versao,
        anuncio.Preco, anuncio.Descricao, anuncio.Estado, anuncio.Cidade, anuncio.Status,
        anuncio.Fotos.OrderBy(f => f.Ordem).Select(f => new FotoResponse(f.Id, f.Url)).ToList(), anuncio.Patrocinado,
        anuncio.Visualizacoes);

    [HttpPost]
    [RequestSizeLimit(60_000_000)]
    public async Task<ActionResult<AnuncioResponse>> Criar([FromForm] CriarAnuncioRequest request)
    {
        // VendedorId/Tipo nunca vêm do corpo da requisição — sempre das claims do próprio token validado,
        // nunca do cliente (aceitar isso do corpo permitiria criar um Anúncio em nome de outro Vendedor).
        // TryParse em vez de Parse — primeiro endpoint do app a ler claims; se algum dia o formato do
        // token divergir (claim renomeada, emissor diferente aceito), isso vira 401 controlado em vez
        // de uma exceção não tratada (500) no meio da requisição
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId) ||
            !Enum.TryParse<TipoVendedor>(User.FindFirstValue("tipo"), out var tipo))
        {
            return Unauthorized();
        }

        var fotos = request.Fotos ?? [];

        var erroValidacao = ValidarFotos(fotos);
        if (erroValidacao is not null)
            return erroValidacao;

        try
        {
            var arquivos = fotos
                .Select(f => new ArquivoFoto(f.OpenReadStream(), f.FileName, f.ContentType))
                .ToList();

            var input = new CriarAnuncioInput(
                vendedorId, tipo, request.Publicar,
                request.Marca, request.Modelo, request.Ano, request.Versao,
                request.Preco, request.Descricao, request.Estado, request.Cidade,
                arquivos);

            var anuncio = await _criarAnuncioService.CriarAsync(input);

            // CreatedAtAction agora que existe um GET /api/anuncios/{id} de verdade nesta story (Task 4) —
            // diferente das Stories 1.2/1.3, o Location header aponta pra uma rota que responde de fato
            return CreatedAtAction(nameof(Obter), new { id = anuncio.Id }, ParaResponse(anuncio));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
        }
        catch (LimiteDeAnunciosAtivosExcedidoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Limite de Anúncios ativos excedido");
        }
    }

    // Story 2.8 — adiciona fotos a um Anúncio já existente. O limite TOTAL (fotos já existentes +
    // este lote) não é validado aqui — só a Application tem o Anuncio carregado pra saber quantas
    // já existem (ver GerenciarAnuncioService.AdicionarFotosAsync/LimiteDeFotosExcedidoException);
    // este endpoint só valida a forma de cada arquivo do lote recebido, igual Criar já fazia
    [HttpPost("{id:guid}/fotos")]
    [RequestSizeLimit(60_000_000)]
    public async Task<ActionResult<AnuncioResponse>> AdicionarFotos(Guid id, [FromForm] AdicionarFotosRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        var fotos = request.Fotos ?? [];

        // achado no code review: diferente de Criar (onde Fotos vazio é uma Ficha sem fotos, válida
        // por FR-4), chamar ESTE endpoint sem nenhum arquivo não tem leitura válida — só geraria uma
        // escrita/SaveChangesAsync à toa. O frontend já desabilita o botão nesse caso; isto cobre
        // qualquer chamada direta à API
        if (fotos.Count == 0)
        {
            return Problem(
                detail: "Nenhuma foto enviada.",
                statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
        }

        var erroValidacao = ValidarFotos(fotos);
        if (erroValidacao is not null)
            return erroValidacao;

        try
        {
            var arquivos = fotos
                .Select(f => new ArquivoFoto(f.OpenReadStream(), f.FileName, f.ContentType))
                .ToList();

            var anuncio = await _gerenciarAnuncioService.AdicionarFotosAsync(id, vendedorId, arquivos);
            return Ok(ParaResponse(anuncio));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
        }
        catch (AnuncioNaoEncontradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Anúncio não encontrado");
        }
        catch (AnuncioNaoPertenceAoVendedorException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Acesso negado");
        }
        catch (LimiteDeFotosExcedidoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Limite de fotos excedido");
        }
    }

    [HttpDelete("{id:guid}/fotos/{fotoId:guid}")]
    public async Task<ActionResult<AnuncioResponse>> RemoverFoto(Guid id, Guid fotoId)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        try
        {
            var anuncio = await _gerenciarAnuncioService.RemoverFotoAsync(id, vendedorId, fotoId);
            return Ok(ParaResponse(anuncio));
        }
        catch (AnuncioNaoEncontradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Anúncio não encontrado");
        }
        catch (AnuncioNaoPertenceAoVendedorException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Acesso negado");
        }
        catch (FotoNaoEncontradaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Foto não encontrada");
        }
    }

    // extraído de Criar (Story 2.1) pra ser reaproveitado por AdicionarFotos (Story 2.8) — valida só
    // a FORMA do lote recebido nesta requisição (quantidade do lote, tamanho, tipo); o limite TOTAL
    // (existentes + novas) é responsabilidade da Application, não deste helper
    private ActionResult? ValidarFotos(List<IFormFile> fotos)
    {
        if (fotos.Count > MaxFotos)
        {
            return Problem(
                detail: $"Máximo de {MaxFotos} fotos por Anúncio.",
                statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
        }

        foreach (var foto in fotos)
        {
            if (foto.Length == 0)
            {
                return Problem(
                    detail: "Arquivo de foto vazio.",
                    statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
            }

            if (foto.Length > MaxTamanhoFotoBytes)
            {
                return Problem(
                    detail: $"Cada foto deve ter no máximo {MaxTamanhoFotoBytes / (1024 * 1024)} MB.",
                    statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
            }

            var extensao = Path.GetExtension(foto.FileName);
            if (!ExtensoesPermitidas.Contains(extensao) || !TiposConteudoPermitidos.Contains(foto.ContentType))
            {
                return Problem(
                    detail: "Tipo de arquivo não suportado. Envie apenas .jpg, .jpeg, .png ou .webp.",
                    statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
            }
        }

        return null;
    }
}
