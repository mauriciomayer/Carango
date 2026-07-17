namespace Carango.Api.Contracts;

public class CriarAnuncioRequest
{
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public int? Ano { get; set; }
    public string? Versao { get; set; }
    public decimal? Preco { get; set; }
    public string? Descricao { get; set; }
    public string? Estado { get; set; }
    public string? Cidade { get; set; }
    public bool Publicar { get; set; }
    public List<IFormFile>? Fotos { get; set; }
}
