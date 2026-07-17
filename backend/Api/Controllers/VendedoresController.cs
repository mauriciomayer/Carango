using Carango.Api.Contracts;
using Carango.Application;
using Microsoft.AspNetCore.Mvc;

namespace Carango.Api.Controllers;

[ApiController]
[Route("api/vendedores")]
public class VendedoresController : ControllerBase
{
    private readonly CadastrarVendedorService _cadastrarVendedorService;
    private readonly AutenticarVendedorService _autenticarVendedorService;

    public VendedoresController(CadastrarVendedorService cadastrarVendedorService, AutenticarVendedorService autenticarVendedorService)
    {
        _cadastrarVendedorService = cadastrarVendedorService;
        _autenticarVendedorService = autenticarVendedorService;
    }

    [HttpPost]
    public async Task<ActionResult<VendedorResponse>> Cadastrar(CadastroVendedorRequest request)
    {
        try
        {
            var input = new CadastrarVendedorInput(request.Email, request.Senha, request.Tipo, request.Telefone, request.CnpjRazaoSocial);
            var vendedor = await _cadastrarVendedorService.CadastrarAsync(input);

            var response = new VendedorResponse(vendedor.Id, vendedor.Email, vendedor.Tipo);
            // Created(string, ...) em vez de CreatedAtAction — não existe endpoint GET por id ainda (fora do escopo desta story);
            // CreatedAtAction exigiria uma action correspondente e lançaria em tempo de execução ao gerar o Location header.
            return Created($"/api/vendedores/{vendedor.Id}", response);
        }
        catch (EmailJaCadastradoException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "E-mail já cadastrado");
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida");
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginVendedorResponse>> Login(LoginVendedorRequest request)
    {
        try
        {
            var input = new AutenticarVendedorInput(request.Email, request.Senha);
            var (vendedor, token) = await _autenticarVendedorService.AutenticarAsync(input);

            var response = new LoginVendedorResponse(
                token.Token,
                token.ExpiraEmUtc,
                new VendedorResponse(vendedor.Id, vendedor.Email, vendedor.Tipo));

            return Ok(response);
        }
        catch (CredenciaisInvalidasException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Credenciais inválidas");
        }
    }
}
