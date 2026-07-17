using Carango.Domain;

namespace Carango.Application;

public record ArquivoFoto(Stream Conteudo, string NomeArquivo, string TipoConteudo);

public record CriarAnuncioInput(
    Guid VendedorId,
    TipoVendedor TipoVendedor,
    bool Publicar,
    string? Marca,
    string? Modelo,
    int? Ano,
    string? Versao,
    decimal? Preco,
    string? Descricao,
    string? Estado,
    string? Cidade,
    IReadOnlyList<ArquivoFoto> Fotos);
