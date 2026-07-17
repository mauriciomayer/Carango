namespace Carango.Api.Contracts;

// Contrato novo, não reaproveita AnuncioBuscaResponse (que omite Descricao de propósito,
// Story 3.1) nem AnuncioResponse (que inclui VendedorId/Status, sem uso público ainda).
// Descricao incluída aqui porque a AC exige "todos os campos da Ficha, sem omissão"
public record AnuncioDetalheResponse(
    Guid Id,
    string? Marca,
    string? Modelo,
    int? Ano,
    string? Versao,
    decimal? Preco,
    string? Descricao,
    string? Estado,
    string? Cidade,
    IReadOnlyList<string> Fotos);
