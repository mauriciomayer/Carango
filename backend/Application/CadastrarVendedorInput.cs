using Carango.Domain;

namespace Carango.Application;

public record CadastrarVendedorInput(
    string Email,
    string Senha,
    TipoVendedor Tipo,
    string? Telefone = null,
    string? CnpjRazaoSocial = null);
