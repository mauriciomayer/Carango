using Carango.Application;

namespace Carango.Tests.TestDoubles;

public class FakeMediaStorage : IMediaStorage
{
    public List<string> ArquivosSalvos { get; } = new();

    public Task<string> SalvarAsync(Stream conteudo, string nomeArquivoOriginal, string tipoConteudo, CancellationToken cancellationToken = default)
    {
        var url = $"/uploads/anuncios/fake-{Guid.NewGuid()}-{nomeArquivoOriginal}";
        ArquivosSalvos.Add(url);
        return Task.FromResult(url);
    }

    public Task ExcluirAsync(string url, CancellationToken cancellationToken = default)
    {
        ArquivosSalvos.Remove(url);
        return Task.CompletedTask;
    }
}
