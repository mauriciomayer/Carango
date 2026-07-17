using Carango.Domain;

namespace Carango.Api.Contracts;

public record AnuncioResponse(
    Guid Id,
    Guid VendedorId,
    string? Marca,
    string? Modelo,
    int? Ano,
    string? Versao,
    decimal? Preco,
    string? Descricao,
    string? Estado,
    string? Cidade,
    StatusAnuncio Status,
    IReadOnlyList<FotoResponse> Fotos,
    bool Patrocinado,
    int Visualizacoes);
