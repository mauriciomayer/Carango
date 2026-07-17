namespace Carango.Application;

public record EditarAnuncioInput(
    Guid AnuncioId,
    Guid VendedorId,
    string? Marca,
    string? Modelo,
    int? Ano,
    string? Versao,
    decimal? Preco,
    string? Descricao,
    string? Estado,
    string? Cidade);
