using System.Text.RegularExpressions;
using Carango.Api.Contracts;
using Carango.Application;
using Microsoft.AspNetCore.Mvc;

namespace Carango.Api.Controllers;

// Sem [Authorize] — dado de referência público, sem posse/autorização (mesmo espírito de
// BuscaController). Endpoint reaproveitado pela Story 3.7 (filtro de Busca, backlog) além de
// Criar/Editar Anúncio (Story 2.6) — ver Contexto da story pra detalhes
[ApiController]
[Route("api/veiculos-referencia")]
public class VeiculosReferenciaController : ControllerBase
{
    private readonly VeiculoReferenciaService _veiculoReferenciaService;

    public VeiculosReferenciaController(VeiculoReferenciaService veiculoReferenciaService)
    {
        _veiculoReferenciaService = veiculoReferenciaService;
    }

    [HttpGet("marcas")]
    public async Task<ActionResult<List<VeiculoReferenciaResponse>>> Marcas()
    {
        try
        {
            var marcas = await _veiculoReferenciaService.ListarMarcasAsync();
            return Ok(marcas.Select(ParaResponse).ToList());
        }
        catch (VeiculoReferenciaIndisponivelException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Serviço indisponível");
        }
    }

    [HttpGet("modelos")]
    public async Task<ActionResult<List<VeiculoReferenciaResponse>>> Modelos([FromQuery] string? marca)
    {
        // servidor é a fonte de verdade (AD-1) — não confia só na cascata já imposta pelo
        // frontend (AC #2); pedir modelos sem marca não faz sentido em nenhum caso
        if (string.IsNullOrWhiteSpace(marca))
            return Problem(detail: "O parâmetro 'marca' é obrigatório.", statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");

        // achado no code review: código de marca da Fipe é sempre numérico (confirmado na
        // pesquisa da API real) — validar o formato aqui rejeita entrada malformada antes de
        // virar segmento de path na chamada pra Fipe (Infrastructure), em vez de deixar um
        // valor arbitrário alcançar o HttpClient
        if (!Regex.IsMatch(marca.Trim(), "^[0-9]+$"))
            return Problem(detail: "O parâmetro 'marca' deve ser um código numérico.", statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");

        try
        {
            var modelos = await _veiculoReferenciaService.ListarModelosAsync(marca.Trim());
            return Ok(modelos.Select(ParaResponse).ToList());
        }
        catch (VeiculoReferenciaIndisponivelException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Serviço indisponível");
        }
    }

    private static VeiculoReferenciaResponse ParaResponse(VeiculoReferenciaItem item) => new(item.Codigo, item.Nome);
}
