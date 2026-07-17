namespace Carango.Api.Contracts;

// PUT com semântica de substituição completa da Ficha — um campo omitido/null aqui apaga o valor
// existente (Anuncio.AtualizarFicha aplica todos os 8 campos incondicionalmente). Não é um PATCH
// parcial; o chamador sempre precisa enviar o estado completo do formulário.
public record EditarAnuncioRequest(
    string? Marca,
    string? Modelo,
    int? Ano,
    string? Versao,
    decimal? Preco,
    string? Descricao,
    string? Estado,
    string? Cidade);
