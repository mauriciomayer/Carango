namespace Carango.Api.Contracts;

// Contrato novo, não reaproveita AnuncioResponse: sem VendedorId (sem uso público ainda,
// nenhuma story consome isso — Detalhe é a Story 3.5, Contato é a 3.6) e sem Status
// (sempre Ativo por construção nesta busca, redundante expor)
public record AnuncioBuscaResponse(
    Guid Id,
    string? Marca,
    string? Modelo,
    int? Ano,
    string? Versao,
    decimal? Preco,
    string? Estado,
    string? Cidade,
    IReadOnlyList<string> Fotos,
    bool Patrocinado);
