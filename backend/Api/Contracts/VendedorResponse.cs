using Carango.Domain;

namespace Carango.Api.Contracts;

public record VendedorResponse(Guid Id, string Email, TipoVendedor Tipo);
