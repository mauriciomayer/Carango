namespace Carango.Application;

public record FiltroBusca(
    string? Marca = null,
    string? Modelo = null,
    int? Ano = null,
    string? Versao = null,
    decimal? PrecoMin = null,
    decimal? PrecoMax = null,
    string? Estado = null,
    string? Cidade = null,
    OrdenacaoBusca Ordenacao = OrdenacaoBusca.Relevancia,
    string? TermoLivre = null,
    int Pagina = 1);
