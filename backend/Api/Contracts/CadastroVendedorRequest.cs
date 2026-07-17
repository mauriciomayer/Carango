using Carango.Domain;

namespace Carango.Api.Contracts;

public record CadastroVendedorRequest(
    string Email,
    string Senha,
    TipoVendedor Tipo,
    string? Telefone = null,
    string? CnpjRazaoSocial = null);
