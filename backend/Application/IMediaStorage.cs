namespace Carango.Application;

public interface IMediaStorage
{
    Task<string> SalvarAsync(Stream conteudo, string nomeArquivoOriginal, string tipoConteudo, CancellationToken cancellationToken = default);

    // usado como limpeza de melhor esforço quando uma foto já salva precisa ser descartada porque o
    // restante da operação (outra foto, ou a persistência do Anuncio) falhou depois — ver CriarAnuncioService
    Task ExcluirAsync(string url, CancellationToken cancellationToken = default);
}
