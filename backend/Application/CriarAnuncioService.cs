using Carango.Domain;

namespace Carango.Application;

public class CriarAnuncioService
{
    private readonly IAnuncioRepository _repositorio;
    private readonly IMediaStorage _mediaStorage;
    private readonly IPlanoLojistaRepository _planoRepositorio;

    public CriarAnuncioService(IAnuncioRepository repositorio, IMediaStorage mediaStorage, IPlanoLojistaRepository planoRepositorio)
    {
        _repositorio = repositorio;
        _mediaStorage = mediaStorage;
        _planoRepositorio = planoRepositorio;
    }

    public async Task<Anuncio> CriarAsync(CriarAnuncioInput input)
    {
        var anuncio = Anuncio.CriarRascunho(
            input.VendedorId, input.Marca, input.Modelo, input.Ano, input.Versao,
            input.Preco, input.Descricao, input.Estado, input.Cidade);

        if (input.Publicar)
        {
            // O limite de 1 Anúncio ativo vale pra QUALQUER Vendedor, exceto Lojista com Plano Lojista
            // ativo (Story 4.2, AC #1/#2) — `input.TipoVendedor` era mantido no input desde a Story 2.1
            // justamente pra essa checagem chegar aqui algum dia.
            var isento = input.TipoVendedor == TipoVendedor.Lojista && await _planoRepositorio.TemPlanoAtivoAsync(input.VendedorId);
            if (!isento && await _repositorio.ContarAtivosPorVendedorAsync(input.VendedorId) >= 1)
                throw new LimiteDeAnunciosAtivosExcedidoException();

            anuncio.Publicar();
        }

        var urlsSalvas = new List<string>();
        try
        {
            foreach (var arquivo in input.Fotos)
            {
                var url = await _mediaStorage.SalvarAsync(arquivo.Conteudo, arquivo.NomeArquivo, arquivo.TipoConteudo);
                urlsSalvas.Add(url);
                anuncio.AdicionarFoto(url);
            }

            await _repositorio.AdicionarAsync(anuncio);
        }
        catch
        {
            // limpeza de melhor esforço — se uma foto no meio da lista ou a persistência do Anuncio
            // falhar depois, as fotos já salvas neste request não ficam órfãs em disco. Falha na
            // própria limpeza é ignorada de propósito: a exceção original é o que importa propagar
            foreach (var url in urlsSalvas)
            {
                try
                {
                    await _mediaStorage.ExcluirAsync(url);
                }
                catch
                {
                    // melhor esforço — nada a fazer se a limpeza também falhar
                }
            }

            throw;
        }

        return anuncio;
    }
}
