using Carango.Domain;

namespace Carango.Application;

public class GerenciarAnuncioService
{
    // valor provisório — modelo de cobrança real (Pergunta Aberta 3 do PRD) ainda não definido
    // pelo cliente; qualquer valor fixo aqui é só o suficiente pra exercitar o fluxo de
    // "marcar como patrocinado" com o MockBillingGateway (Story 4.1)
    private const decimal ValorDestaque = 29.90m;

    // mesmo valor de AnunciosController.MaxFotos (Story 2.1) — duplicado deliberadamente aqui
    // porque só a Application tem o Anuncio carregado (com Fotos já existentes) pra saber se
    // "existentes + novas" excede o limite; o Controller só valida o lote recebido na requisição
    private const int MaxFotos = 10;

    private readonly IAnuncioRepository _repositorio;
    private readonly IMediaStorage _mediaStorage;
    private readonly IBillingGateway _billingGateway;
    private readonly IVendedorRepository _vendedorRepositorio;
    private readonly IPlanoLojistaRepository _planoRepositorio;

    public GerenciarAnuncioService(
        IAnuncioRepository repositorio, IMediaStorage mediaStorage, IBillingGateway billingGateway,
        IVendedorRepository vendedorRepositorio, IPlanoLojistaRepository planoRepositorio)
    {
        _repositorio = repositorio;
        _mediaStorage = mediaStorage;
        _billingGateway = billingGateway;
        _vendedorRepositorio = vendedorRepositorio;
        _planoRepositorio = planoRepositorio;
    }

    public Task<Anuncio> ObterParaEdicaoAsync(Guid anuncioId, Guid vendedorId) =>
        ObterEValidarPosseAsync(anuncioId, vendedorId);

    public async Task<Anuncio> EditarAsync(EditarAnuncioInput input)
    {
        var anuncio = await ObterEValidarPosseAsync(input.AnuncioId, input.VendedorId);

        anuncio.AtualizarFicha(
            input.Marca, input.Modelo, input.Ano, input.Versao,
            input.Preco, input.Descricao, input.Estado, input.Cidade);

        await _repositorio.AtualizarAsync(anuncio);

        return anuncio;
    }

    public async Task<Anuncio> PausarAsync(Guid anuncioId, Guid vendedorId)
    {
        var anuncio = await ObterEValidarPosseAsync(anuncioId, vendedorId);

        anuncio.Pausar();
        await _repositorio.AtualizarAsync(anuncio);

        return anuncio;
    }

    public async Task<Anuncio> ReativarAsync(Guid anuncioId, Guid vendedorId)
    {
        var anuncio = await ObterEValidarPosseAsync(anuncioId, vendedorId);

        // a checagem de cota só roda quando o Anúncio realmente está Pausado (elegível pra reativar).
        // Se rodasse incondicionalmente, reativar um Anúncio que JÁ está Ativo seria classificado
        // errado: a contagem incluiria o próprio Anúncio (que já está Ativo no repositório), sempre
        // resultando em "limite excedido" em vez da mensagem correta de transição inválida — achado
        // no code review, as 3 camadas de revisão pegaram independentemente. Só quando é Pausado de
        // verdade a reativação consome a cota de 1-Anúncio-ativo (mesma exceção/padrão já usado em
        // CriarAnuncioService na Story 2.1, reaproveitada aqui, AD-11); caso contrário, deixa
        // anuncio.Reativar() lançar sua própria InvalidOperationException, sem gastar uma consulta à toa.
        // Lojista com Plano Lojista ativo é isento (Story 4.2, AC #1/#2) — mesma regra de
        // CriarAnuncioService. ContarAtivosPorVendedorAsync roda ANTES de TemIsencaoDoLimiteAsync
        // (2 consultas extras) — achado no code review: no caso comum (sem outro Anúncio ativo pra
        // conflitar), o count-check barato já resolve pra false e evita as consultas extras de
        // Vendedor/PlanoLojista, que só importam quando existe de fato um conflito a resolver
        if (anuncio.Status == StatusAnuncio.Pausado &&
            await _repositorio.ContarAtivosPorVendedorAsync(vendedorId) >= 1 && !await TemIsencaoDoLimiteAsync(vendedorId))
            throw new LimiteDeAnunciosAtivosExcedidoException();

        anuncio.Reativar();
        await _repositorio.AtualizarAsync(anuncio);

        return anuncio;
    }

    public async Task<Anuncio> DestacarAsync(Guid anuncioId, Guid vendedorId)
    {
        var anuncio = await ObterEValidarPosseAsync(anuncioId, vendedorId);

        // anuncio.Destacar() abaixo já rejeita status != Ativo — a cobrança só roda depois dessa
        // checagem estar implicitamente garantida (Destacar() lança antes de aplicar Patrocinado),
        // mas cobrar ANTES de confirmar o Anúncio elegível cobraria por uma transição que nunca
        // aconteceu. Verificação de status explícita aqui evita cobrar por nada.
        if (anuncio.Status != StatusAnuncio.Ativo)
            throw new InvalidOperationException($"Só é possível destacar um Anúncio a partir do status Ativo (status atual: {anuncio.Status}).");

        // achado no code review: sem isso, destacar um Anúncio já patrocinado cobrava de novo por
        // um "destaque" que já existia — o frontend evita isso escondendo o botão, mas nada no
        // Application/Domain impedia uma chamada direta à API de cobrar duas vezes pela mesma coisa
        if (anuncio.Patrocinado)
            throw new InvalidOperationException("Este Anúncio já está patrocinado.");

        var resultado = await _billingGateway.CobrarAsync(vendedorId, $"Destaque do Anúncio {anuncio.Id}", ValorDestaque);
        if (!resultado.Sucesso)
            throw new CobrancaFalhouException(resultado.MotivoFalha ?? "Pagamento não autorizado.");

        anuncio.Destacar();
        await _repositorio.AtualizarAsync(anuncio);

        return anuncio;
    }

    public async Task<Anuncio> MarcarComoVendidoAsync(Guid anuncioId, Guid vendedorId)
    {
        var anuncio = await ObterEValidarPosseAsync(anuncioId, vendedorId);

        anuncio.MarcarComoVendido();
        await _repositorio.AtualizarAsync(anuncio);

        return anuncio;
    }

    // mesmo padrão de limpeza de melhor esforço de CriarAnuncioService.CriarAsync — se uma foto no
    // meio do lote ou a persistência falhar depois, as fotos já salvas NESTA chamada não ficam
    // órfãs em disco. Falha na própria limpeza é ignorada de propósito, a exceção original importa
    public async Task<Anuncio> AdicionarFotosAsync(Guid anuncioId, Guid vendedorId, IReadOnlyList<ArquivoFoto> fotos)
    {
        var anuncio = await ObterEValidarPosseAsync(anuncioId, vendedorId);

        if (anuncio.Fotos.Count + fotos.Count > MaxFotos)
            throw new LimiteDeFotosExcedidoException(anuncio.Fotos.Count, fotos.Count);

        var urlsSalvas = new List<string>();
        try
        {
            foreach (var arquivo in fotos)
            {
                var url = await _mediaStorage.SalvarAsync(arquivo.Conteudo, arquivo.NomeArquivo, arquivo.TipoConteudo);
                urlsSalvas.Add(url);
                anuncio.AdicionarFoto(url);
            }

            await _repositorio.AtualizarAsync(anuncio);
        }
        catch
        {
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

    public async Task<Anuncio> RemoverFotoAsync(Guid anuncioId, Guid vendedorId, Guid fotoId)
    {
        var anuncio = await ObterEValidarPosseAsync(anuncioId, vendedorId);

        var url = anuncio.RemoverFoto(fotoId);
        if (url is null)
            throw new FotoNaoEncontradaException();

        // exclui do banco primeiro — mesma ordem e mesma razão de ExcluirAsync: se a limpeza do
        // storage abaixo falhar, o pior caso é um arquivo órfão em disco (recuperável); na ordem
        // inversa, uma falha no banco deixaria a Foto "fantasma" já removida do storage mas ainda
        // referenciada
        await _repositorio.AtualizarAsync(anuncio);

        try
        {
            await _mediaStorage.ExcluirAsync(url);
        }
        catch
        {
            // melhor esforço — nada a fazer se a limpeza também falhar
        }

        return anuncio;
    }

    public async Task ExcluirAsync(Guid anuncioId, Guid vendedorId)
    {
        var anuncio = await ObterEValidarPosseAsync(anuncioId, vendedorId);

        // exclui do banco primeiro — se a limpeza de arquivo abaixo falhar depois, o pior caso é um
        // arquivo órfão em disco (recuperável); se fosse na ordem inversa e o delete do banco falhasse
        // depois de já ter apagado os arquivos, o Anúncio continuaria existindo com Fotos quebradas
        await _repositorio.ExcluirAsync(anuncio);

        // anuncio.Fotos continua populado em memória mesmo depois do Remove/SaveChangesAsync (só marca
        // as entidades como excluídas no DbContext, não limpa a coleção em memória) — limpeza de
        // melhor esforço, resolve o item deferido desde a Story 2.1 sobre arquivos físicos órfãos
        foreach (var foto in anuncio.Fotos)
        {
            try
            {
                await _mediaStorage.ExcluirAsync(foto.Url);
            }
            catch
            {
                // melhor esforço — nada a fazer se a limpeza também falhar
            }
        }
    }

    // sem checagem de posse aqui — o Vendedor sempre pode listar os próprios Anúncios, não há "id de
    // outro" pra verificar (diferente de editar/pausar/etc., que agem sobre um Anuncio específico)
    public Task<IReadOnlyList<Anuncio>> ListarAsync(Guid vendedorId) =>
        _repositorio.ListarPorVendedorAsync(vendedorId);

    // único ponto da Application que verifica posse sobre um Anuncio (AD-9) — reaproveitado pela
    // leitura (ObterParaEdicaoAsync) e por toda escrita (EditarAsync, PausarAsync, ReativarAsync,
    // MarcarComoVendidoAsync, ExcluirAsync)
    private async Task<Anuncio> ObterEValidarPosseAsync(Guid anuncioId, Guid vendedorId)
    {
        var anuncio = await _repositorio.ObterPorIdAsync(anuncioId);

        if (anuncio is null)
            throw new AnuncioNaoEncontradoException();

        if (anuncio.VendedorId != vendedorId)
            throw new AnuncioNaoPertenceAoVendedorException();

        return anuncio;
    }

    // Lojista com Plano Lojista ativo é isento do limite de 1 Anúncio ativo (Story 4.2, AC #1/#2).
    // Diferente de CriarAnuncioService (que já recebe TipoVendedor no input), este serviço só tem
    // vendedorId — busca o Vendedor pra saber o Tipo antes de checar o plano
    private async Task<bool> TemIsencaoDoLimiteAsync(Guid vendedorId)
    {
        var vendedor = await _vendedorRepositorio.ObterPorIdAsync(vendedorId);
        return vendedor?.Tipo == TipoVendedor.Lojista && await _planoRepositorio.TemPlanoAtivoAsync(vendedorId);
    }
}
