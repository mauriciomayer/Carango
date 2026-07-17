using Carango.Application;

namespace Carango.Infrastructure;

// Implementação interina enquanto o provedor de storage real (S3/Azure Blob/MinIO) não é escolhido
// (ARCHITECTURE-SPINE.md § AD-3, § Deferred). Trocável por outra implementação de IMediaStorage sem
// tocar Domain/Application quando a hospedagem for decidida.
public class LocalDiskMediaStorage : IMediaStorage
{
    private const int TamanhoBufferPadrao = 4096;

    private readonly string _caminhoBase;

    public LocalDiskMediaStorage(string caminhoBase)
    {
        _caminhoBase = caminhoBase;
        Directory.CreateDirectory(_caminhoBase);
    }

    public async Task<string> SalvarAsync(Stream conteudo, string nomeArquivoOriginal, string tipoConteudo, CancellationToken cancellationToken = default)
    {
        // nome de arquivo do upload nunca é confiável (path traversal, colisão) — só a extensão original
        // (já validada pelo Controller) é preservada; o nome em si é sempre um novo GUID gerado aqui
        var extensao = Path.GetExtension(nomeArquivoOriginal);
        var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
        var caminhoCompleto = Path.Combine(_caminhoBase, nomeArquivo);

        // FileOptions.Asynchronous — File.Create abriria o handle de forma síncrona mesmo dentro
        // deste método async, prendendo uma thread do pool nesse I/O sem necessidade
        await using (var arquivo = new FileStream(
            caminhoCompleto, FileMode.Create, FileAccess.Write, FileShare.None, TamanhoBufferPadrao, FileOptions.Asynchronous))
        {
            await conteudo.CopyToAsync(arquivo, cancellationToken);
        }

        return $"/uploads/anuncios/{nomeArquivo}";
    }

    public Task ExcluirAsync(string url, CancellationToken cancellationToken = default)
    {
        var nomeArquivo = Path.GetFileName(url);
        var caminhoCompleto = Path.Combine(_caminhoBase, nomeArquivo);

        if (File.Exists(caminhoCompleto))
            File.Delete(caminhoCompleto);

        return Task.CompletedTask;
    }
}
