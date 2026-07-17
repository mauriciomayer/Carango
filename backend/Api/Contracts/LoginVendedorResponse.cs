namespace Carango.Api.Contracts;

public record LoginVendedorResponse(string Token, DateTime ExpiraEm, VendedorResponse Vendedor);
