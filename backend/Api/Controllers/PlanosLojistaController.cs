using System.Security.Claims;
using Carango.Api.Contracts;
using Carango.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carango.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/planos-lojista")]
public class PlanosLojistaController : ControllerBase
{
    private readonly AssinarPlanoLojistaService _assinarPlanoLojistaService;
    private readonly GerenciarPlanoLojistaService _gerenciarPlanoLojistaService;

    public PlanosLojistaController(
        AssinarPlanoLojistaService assinarPlanoLojistaService, GerenciarPlanoLojistaService gerenciarPlanoLojistaService)
    {
        _assinarPlanoLojistaService = assinarPlanoLojistaService;
        _gerenciarPlanoLojistaService = gerenciarPlanoLojistaService;
    }

    [HttpGet("meu")]
    public async Task<ActionResult<PlanoLojistaResponse>> Meu()
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        var plano = await _gerenciarPlanoLojistaService.ObterAsync(vendedorId);
        // 404 sem Problem() — não ter um Plano Lojista não é um erro de negócio a explicar
        // (diferente de PlanoLojistaNaoEncontradoException em Cancelar, que É um erro porque o
        // Vendedor pediu uma ação que pressupõe um plano existir), é só a ausência do recurso
        if (plano is null)
            return NotFound();

        return Ok(new PlanoLojistaResponse(plano.Id, plano.Status));
    }

    [HttpPost("cancelar")]
    public async Task<ActionResult<PlanoLojistaResponse>> Cancelar()
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        try
        {
            var plano = await _gerenciarPlanoLojistaService.CancelarAsync(vendedorId);
            return Ok(new PlanoLojistaResponse(plano.Id, plano.Status));
        }
        catch (PlanoLojistaNaoEncontradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Plano Lojista não encontrado");
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Transição de plano inválida");
        }
    }

    [HttpPost("assinar")]
    public async Task<ActionResult<PlanoLojistaResponse>> Assinar()
    {
        // VendedorId nunca vem do corpo da requisição — sempre das claims do próprio token validado
        // (AD-9), mesmo padrão já usado em AnunciosController
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var vendedorId))
            return Unauthorized();

        try
        {
            var plano = await _assinarPlanoLojistaService.AssinarAsync(vendedorId);
            // 201, não 200 — "assinar" cria um PlanoLojista novo (mesma semântica REST de
            // VendedoresController.Cadastrar), diferente de pausar/reativar/destacar (200, transições
            // sobre um recurso já existente). Sem GET /api/planos-lojista/{id} ainda, então Created(string, ...)
            // em vez de CreatedAtAction — mesmo padrão já usado em VendedoresController.Cadastrar
            return Created($"/api/planos-lojista/{plano.Id}", new PlanoLojistaResponse(plano.Id, plano.Status));
        }
        catch (VendedorNaoELojistaException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Operação não permitida");
        }
        catch (PlanoLojistaJaAtivoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Plano Lojista já ativo");
        }
        catch (CobrancaFalhouException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Pagamento não concluído");
        }
    }
}
