using Carango.Application;
using Carango.Domain;
using Carango.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class GerenciarAnuncioServiceTests
{
    private static readonly Guid VendedorId = Guid.NewGuid();
    private static readonly Guid OutroVendedorId = Guid.NewGuid();

    private static async Task<(FakeAnuncioRepository Repositorio, GerenciarAnuncioService Service, Anuncio Anuncio)> CenarioComAnuncio()
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        await repositorio.AdicionarAsync(anuncio);
        return (repositorio, new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository()), anuncio);
    }

    private static async Task<(FakeAnuncioRepository Repositorio, GerenciarAnuncioService Service, Anuncio Anuncio)> CenarioComAnuncioAtivo()
    {
        var (repositorio, service, anuncio) = await CenarioComAnuncio();
        anuncio.Publicar();
        return (repositorio, service, anuncio);
    }

    private static async Task<(FakeAnuncioRepository Repositorio, GerenciarAnuncioService Service, Anuncio Anuncio)> CenarioComAnuncioPausado()
    {
        var (repositorio, service, anuncio) = await CenarioComAnuncioAtivo();
        anuncio.Pausar();
        return (repositorio, service, anuncio);
    }

    [Fact]
    public async Task EditarAsync_ComDonoCorretoECamposValidos_AtualizaOAnuncio()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();
        var input = new EditarAnuncioInput(
            anuncio.Id, VendedorId, "Toyota", "Corolla", 2021, "XEI", 110000m, "Revisado", "RJ", "Rio de Janeiro");

        var atualizado = await service.EditarAsync(input);

        atualizado.Marca.ShouldBe("Toyota");
        atualizado.Cidade.ShouldBe("Rio de Janeiro");
    }

    [Fact]
    public async Task EditarAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());
        var input = new EditarAnuncioInput(
            Guid.NewGuid(), VendedorId, "Toyota", "Corolla", 2021, "XEI", 110000m, "Revisado", "RJ", "Rio de Janeiro");

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.EditarAsync(input));
    }

    [Fact]
    public async Task EditarAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedor()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();
        var input = new EditarAnuncioInput(
            anuncio.Id, OutroVendedorId, "Toyota", "Corolla", 2021, "XEI", 110000m, "Revisado", "RJ", "Rio de Janeiro");

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() => service.EditarAsync(input));
        anuncio.Marca.ShouldBe("Honda");
    }

    [Fact]
    public async Task ObterParaEdicaoAsync_ComDonoCorreto_RetornaOAnuncio()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();

        var resultado = await service.ObterParaEdicaoAsync(anuncio.Id, VendedorId);

        resultado.ShouldBe(anuncio);
    }

    [Fact]
    public async Task ObterParaEdicaoAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.ObterParaEdicaoAsync(Guid.NewGuid(), VendedorId));
    }

    [Fact]
    public async Task ObterParaEdicaoAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedor()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() =>
            service.ObterParaEdicaoAsync(anuncio.Id, OutroVendedorId));
    }

    [Fact]
    public async Task PausarAsync_ComDonoCorreto_TransicionaParaPausado()
    {
        var (_, service, anuncio) = await CenarioComAnuncioAtivo();

        var atualizado = await service.PausarAsync(anuncio.Id, VendedorId);

        atualizado.Status.ShouldBe(StatusAnuncio.Pausado);
    }

    [Fact]
    public async Task PausarAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.PausarAsync(Guid.NewGuid(), VendedorId));
    }

    [Fact]
    public async Task PausarAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedor()
    {
        var (_, service, anuncio) = await CenarioComAnuncioAtivo();

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() => service.PausarAsync(anuncio.Id, OutroVendedorId));
        anuncio.Status.ShouldBe(StatusAnuncio.Ativo);
    }

    [Fact]
    public async Task ReativarAsync_SemOutroAnuncioAtivo_TransicionaParaAtivo()
    {
        var (_, service, anuncio) = await CenarioComAnuncioPausado();

        var atualizado = await service.ReativarAsync(anuncio.Id, VendedorId);

        atualizado.Status.ShouldBe(StatusAnuncio.Ativo);
    }

    [Fact]
    public async Task ReativarAsync_DeAnuncioJaAtivo_LancaInvalidOperationExceptionNaoLimiteExcedido()
    {
        // regressão: a checagem de cota rodava incondicionalmente antes de anuncio.Reativar() — reativar
        // um Anúncio que JÁ está ativo contava o próprio Anúncio na cota e virava (erradamente)
        // LimiteDeAnunciosAtivosExcedidoException em vez da InvalidOperationException correta de
        // transição inválida. Achado independente pelas 3 camadas de code review.
        var (_, service, anuncio) = await CenarioComAnuncioAtivo();

        await Should.ThrowAsync<InvalidOperationException>(() => service.ReativarAsync(anuncio.Id, VendedorId));
    }

    [Fact]
    public async Task ReativarAsync_ComOutroAnuncioAtivoDoMesmoVendedor_LancaLimiteExcedidoSemAlterar()
    {
        var (repositorio, service, anuncioPausado) = await CenarioComAnuncioPausado();
        var outroAtivo = Anuncio.CriarRascunho(
            VendedorId, marca: "Toyota", modelo: "Corolla", ano: 2020, versao: "XEI",
            preco: 100000m, descricao: "desc", estado: "RJ", cidade: "Rio de Janeiro");
        outroAtivo.Publicar();
        await repositorio.AdicionarAsync(outroAtivo);

        await Should.ThrowAsync<LimiteDeAnunciosAtivosExcedidoException>(() => service.ReativarAsync(anuncioPausado.Id, VendedorId));

        anuncioPausado.Status.ShouldBe(StatusAnuncio.Pausado);
    }

    [Fact]
    public async Task ReativarAsync_LojistaComPlanoAtivoEOutroAnuncioAtivo_ReativaComSucesso()
    {
        // Story 4.2, AC #1: Plano Lojista ativo isenta o limite de 1 Anúncio ativo, também na reativação
        var repositorio = new FakeAnuncioRepository();
        var vendedorRepositorio = new FakeVendedorRepository();
        var lojista = new Vendedor("lojista@exemplo.com", "hash", TipoVendedor.Lojista, cnpjRazaoSocial: "12.345.678/0001-90");
        var lojistaId = lojista.Id;
        vendedorRepositorio.Vendedores.Add(lojista);
        var planoRepositorio = new FakePlanoLojistaRepository();
        planoRepositorio.Planos.Add(PlanoLojista.Assinar(lojistaId));
        var service = new GerenciarAnuncioService(
            repositorio, new FakeMediaStorage(), new FakeBillingGateway(), vendedorRepositorio, planoRepositorio);

        var anuncioPausado = Anuncio.CriarRascunho(
            lojistaId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "desc", estado: "SP", cidade: "São Paulo");
        anuncioPausado.Publicar();
        anuncioPausado.Pausar();
        await repositorio.AdicionarAsync(anuncioPausado);
        var outroAtivo = Anuncio.CriarRascunho(
            lojistaId, marca: "Toyota", modelo: "Corolla", ano: 2020, versao: "XEI",
            preco: 100000m, descricao: "desc", estado: "RJ", cidade: "Rio de Janeiro");
        outroAtivo.Publicar();
        await repositorio.AdicionarAsync(outroAtivo);

        var atualizado = await service.ReativarAsync(anuncioPausado.Id, lojistaId);

        atualizado.Status.ShouldBe(StatusAnuncio.Ativo);
    }

    [Fact]
    public async Task ReativarAsync_LojistaSemPlanoAtivoEOutroAnuncioAtivo_LancaLimiteExcedido()
    {
        // Story 4.2, AC #2: Lojista sem Plano Lojista ativo continua limitado — achado no code
        // review: o teste ComOutroAnuncioAtivoDoMesmoVendedor (acima) usa CenarioComAnuncioPausado,
        // que nunca adiciona um Vendedor ao FakeVendedorRepository — passava por VendedorId
        // desconhecido cair em "não isento" por padrão, não por testar de fato um Lojista sem plano
        var repositorio = new FakeAnuncioRepository();
        var vendedorRepositorio = new FakeVendedorRepository();
        var lojista = new Vendedor("lojista-sem-plano@exemplo.com", "hash", TipoVendedor.Lojista, cnpjRazaoSocial: "12.345.678/0001-90");
        var lojistaId = lojista.Id;
        vendedorRepositorio.Vendedores.Add(lojista);
        var service = new GerenciarAnuncioService(
            repositorio, new FakeMediaStorage(), new FakeBillingGateway(), vendedorRepositorio, new FakePlanoLojistaRepository());

        var anuncioPausado = Anuncio.CriarRascunho(
            lojistaId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "desc", estado: "SP", cidade: "São Paulo");
        anuncioPausado.Publicar();
        anuncioPausado.Pausar();
        await repositorio.AdicionarAsync(anuncioPausado);
        var outroAtivo = Anuncio.CriarRascunho(
            lojistaId, marca: "Toyota", modelo: "Corolla", ano: 2020, versao: "XEI",
            preco: 100000m, descricao: "desc", estado: "RJ", cidade: "Rio de Janeiro");
        outroAtivo.Publicar();
        await repositorio.AdicionarAsync(outroAtivo);

        await Should.ThrowAsync<LimiteDeAnunciosAtivosExcedidoException>(() => service.ReativarAsync(anuncioPausado.Id, lojistaId));

        anuncioPausado.Status.ShouldBe(StatusAnuncio.Pausado);
    }

    [Fact]
    public async Task ReativarAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.ReativarAsync(Guid.NewGuid(), VendedorId));
    }

    [Fact]
    public async Task ReativarAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedor()
    {
        var (_, service, anuncio) = await CenarioComAnuncioPausado();

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() => service.ReativarAsync(anuncio.Id, OutroVendedorId));
        anuncio.Status.ShouldBe(StatusAnuncio.Pausado);
    }

    [Fact]
    public async Task MarcarComoVendidoAsync_DeAtivo_TransicionaParaVendido()
    {
        var (_, service, anuncio) = await CenarioComAnuncioAtivo();

        var atualizado = await service.MarcarComoVendidoAsync(anuncio.Id, VendedorId);

        atualizado.Status.ShouldBe(StatusAnuncio.Vendido);
    }

    [Fact]
    public async Task MarcarComoVendidoAsync_DePausado_TransicionaParaVendido()
    {
        var (_, service, anuncio) = await CenarioComAnuncioPausado();

        var atualizado = await service.MarcarComoVendidoAsync(anuncio.Id, VendedorId);

        atualizado.Status.ShouldBe(StatusAnuncio.Vendido);
    }

    [Fact]
    public async Task MarcarComoVendidoAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.MarcarComoVendidoAsync(Guid.NewGuid(), VendedorId));
    }

    [Fact]
    public async Task MarcarComoVendidoAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedor()
    {
        var (_, service, anuncio) = await CenarioComAnuncioAtivo();

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() => service.MarcarComoVendidoAsync(anuncio.Id, OutroVendedorId));
        anuncio.Status.ShouldBe(StatusAnuncio.Ativo);
    }

    [Fact]
    public async Task ExcluirAsync_ComDonoCorretoSemFotos_RemoveDoRepositorio()
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        await repositorio.AdicionarAsync(anuncio);
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await service.ExcluirAsync(anuncio.Id, VendedorId);

        repositorio.Anuncios.ShouldNotContain(anuncio);
    }

    [Fact]
    public async Task ExcluirAsync_ComFotos_TambemRemoveOsArquivosFisicos()
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");
        anuncio.AdicionarFoto("/uploads/anuncios/foto2.jpg");
        await repositorio.AdicionarAsync(anuncio);
        var mediaStorage = new FakeMediaStorage();
        mediaStorage.ArquivosSalvos.Add("/uploads/anuncios/foto1.jpg");
        mediaStorage.ArquivosSalvos.Add("/uploads/anuncios/foto2.jpg");
        var service = new GerenciarAnuncioService(repositorio, mediaStorage, new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await service.ExcluirAsync(anuncio.Id, VendedorId);

        repositorio.Anuncios.ShouldNotContain(anuncio);
        mediaStorage.ArquivosSalvos.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(StatusAnuncio.Rascunho)]
    [InlineData(StatusAnuncio.Ativo)]
    [InlineData(StatusAnuncio.Pausado)]
    [InlineData(StatusAnuncio.Vendido)]
    public async Task ExcluirAsync_EmQualquerStatus_SempreExclui(StatusAnuncio status)
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        if (status != StatusAnuncio.Rascunho) anuncio.Publicar();
        if (status == StatusAnuncio.Pausado) anuncio.Pausar();
        if (status == StatusAnuncio.Vendido) anuncio.MarcarComoVendido();
        await repositorio.AdicionarAsync(anuncio);
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await service.ExcluirAsync(anuncio.Id, VendedorId);

        repositorio.Anuncios.ShouldNotContain(anuncio);
    }

    [Fact]
    public async Task ExcluirAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.ExcluirAsync(Guid.NewGuid(), VendedorId));
    }

    [Fact]
    public async Task ExcluirAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedorSemExcluir()
    {
        var (repositorio, service, anuncio) = await CenarioComAnuncio();

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() => service.ExcluirAsync(anuncio.Id, OutroVendedorId));

        repositorio.Anuncios.ShouldContain(anuncio);
    }

    [Fact]
    public async Task ListarAsync_RetornaSoOsAnunciosDoVendedorPedidoOrdenadosDoMaisRecenteProMaisAntigo()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());
        // Thread.Sleep entre criações — DateTime.UtcNow no Windows tem resolução de ~15ms; sem o
        // atraso, 3 chamadas seguidas poderiam empatar em CriadoEm e tornar o teste de ordenação instável
        var maisAntigo = Anuncio.CriarRascunho(VendedorId, marca: "Honda", modelo: "Civic");
        await repositorio.AdicionarAsync(maisAntigo);
        Thread.Sleep(20);
        var doMeio = Anuncio.CriarRascunho(VendedorId, marca: "Toyota", modelo: "Corolla");
        await repositorio.AdicionarAsync(doMeio);
        Thread.Sleep(20);
        var maisRecente = Anuncio.CriarRascunho(VendedorId, marca: "Fiat", modelo: "Uno");
        await repositorio.AdicionarAsync(maisRecente);
        var deOutroVendedor = Anuncio.CriarRascunho(OutroVendedorId, marca: "Ford", modelo: "Ka");
        await repositorio.AdicionarAsync(deOutroVendedor);

        var lista = await service.ListarAsync(VendedorId);

        lista.Count.ShouldBe(3);
        lista.ShouldNotContain(deOutroVendedor);
        lista[0].ShouldBe(maisRecente);
        lista[1].ShouldBe(doMeio);
        lista[2].ShouldBe(maisAntigo);
    }

    [Fact]
    public async Task ListarAsync_SemNenhumAnuncio_RetornaListaVazia()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        var lista = await service.ListarAsync(VendedorId);

        lista.ShouldBeEmpty();
    }

    [Fact]
    public async Task DestacarAsync_ComDonoCorretoEAnuncioAtivo_MarcaComoPatrocinado()
    {
        var (_, service, anuncio) = await CenarioComAnuncioAtivo();

        var atualizado = await service.DestacarAsync(anuncio.Id, VendedorId);

        atualizado.Patrocinado.ShouldBeTrue();
    }

    [Fact]
    public async Task DestacarAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedor()
    {
        var (_, service, anuncio) = await CenarioComAnuncioAtivo();

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() => service.DestacarAsync(anuncio.Id, OutroVendedorId));
        anuncio.Patrocinado.ShouldBeFalse();
    }

    [Fact]
    public async Task DestacarAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.DestacarAsync(Guid.NewGuid(), VendedorId));
    }

    [Fact]
    public async Task DestacarAsync_ComAnuncioNaoAtivo_LancaInvalidOperationExceptionSemCobrar()
    {
        var (_, service, anuncio) = await CenarioComAnuncioPausado();
        var billingGateway = new FakeBillingGateway();
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(anuncio);
        var serviceComGatewayRastreavel = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), billingGateway, new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<InvalidOperationException>(() => serviceComGatewayRastreavel.DestacarAsync(anuncio.Id, VendedorId));

        billingGateway.Chamadas.ShouldBeEmpty();
        anuncio.Patrocinado.ShouldBeFalse();
    }

    [Fact]
    public async Task DestacarAsync_ComAnuncioJaPatrocinado_LancaInvalidOperationExceptionSemCobrarDeNovo()
    {
        var (_, _, anuncio) = await CenarioComAnuncioAtivo();
        anuncio.Destacar();
        var billingGateway = new FakeBillingGateway();
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(anuncio);
        var serviceComGatewayRastreavel = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), billingGateway, new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<InvalidOperationException>(() => serviceComGatewayRastreavel.DestacarAsync(anuncio.Id, VendedorId));

        billingGateway.Chamadas.ShouldBeEmpty();
    }

    private static ArquivoFoto FotoFake(string nomeArquivo = "foto.jpg") =>
        new(new MemoryStream([1, 2, 3]), nomeArquivo, "image/jpeg");

    [Fact]
    public async Task AdicionarFotosAsync_ComDonoCorreto_SalvaEAssociaAsFotos()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();

        var atualizado = await service.AdicionarFotosAsync(anuncio.Id, VendedorId, [FotoFake("a.jpg"), FotoFake("b.jpg")]);

        atualizado.Fotos.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AdicionarFotosAsync_ExcedendoOLimiteTotal_LancaLimiteDeFotosExcedidoSemSalvarNenhumArquivo()
    {
        // achado no code review: a versão anterior deste teste usava CenarioComAnuncio(), cujo
        // FakeMediaStorage interno não é exposto — não dava pra provar de fato que SalvarAsync
        // nunca é chamado quando o limite já estoura antes do laço. Monta o cenário manualmente
        // aqui pra ter acesso ao mediaStorage e verificar isso de verdade
        var repositorio = new FakeAnuncioRepository();
        var mediaStorage = new FakeMediaStorage();
        var anuncio = Anuncio.CriarRascunho(VendedorId);
        for (var i = 0; i < 9; i++)
            anuncio.AdicionarFoto($"/uploads/anuncios/existente{i}.jpg");
        await repositorio.AdicionarAsync(anuncio);
        var service = new GerenciarAnuncioService(repositorio, mediaStorage, new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());
        // 9 já existentes + 2 novas = 11, excede o limite de 10 (MaxFotos)

        await Should.ThrowAsync<LimiteDeFotosExcedidoException>(() =>
            service.AdicionarFotosAsync(anuncio.Id, VendedorId, [FotoFake("a.jpg"), FotoFake("b.jpg")]));

        anuncio.Fotos.Count.ShouldBe(9);
        mediaStorage.ArquivosSalvos.ShouldBeEmpty();
    }

    [Fact]
    public async Task AdicionarFotosAsync_NoLimiteExatoDeDez_PermiteAOperacao()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();
        for (var i = 0; i < 8; i++)
            anuncio.AdicionarFoto($"/uploads/anuncios/existente{i}.jpg");
        // 8 já existentes + 2 novas = 10, exatamente no limite

        var atualizado = await service.AdicionarFotosAsync(anuncio.Id, VendedorId, [FotoFake("a.jpg"), FotoFake("b.jpg")]);

        atualizado.Fotos.Count.ShouldBe(10);
    }

    [Fact]
    public async Task AdicionarFotosAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedor()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() =>
            service.AdicionarFotosAsync(anuncio.Id, OutroVendedorId, [FotoFake()]));
        anuncio.Fotos.ShouldBeEmpty();
    }

    [Fact]
    public async Task AdicionarFotosAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() =>
            service.AdicionarFotosAsync(Guid.NewGuid(), VendedorId, [FotoFake()]));
    }

    [Fact]
    public async Task RemoverFotoAsync_ComFotoExistente_RemoveEExcluiDoStorage()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();
        var atualizado = await service.AdicionarFotosAsync(anuncio.Id, VendedorId, [FotoFake("a.jpg"), FotoFake("b.jpg")]);
        var fotoParaRemover = atualizado.Fotos[0];
        var mediaStorage = new FakeMediaStorage();
        mediaStorage.ArquivosSalvos.Add(fotoParaRemover.Url);
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(atualizado);
        var serviceComStorageRastreavel = new GerenciarAnuncioService(repositorio, mediaStorage, new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        var resultado = await serviceComStorageRastreavel.RemoverFotoAsync(atualizado.Id, VendedorId, fotoParaRemover.Id);

        resultado.Fotos.Count.ShouldBe(1);
        mediaStorage.ArquivosSalvos.ShouldNotContain(fotoParaRemover.Url);
    }

    [Fact]
    public async Task RemoverFotoAsync_ComIdDeFotoInexistente_LancaFotoNaoEncontrada()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();
        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");

        await Should.ThrowAsync<FotoNaoEncontradaException>(() =>
            service.RemoverFotoAsync(anuncio.Id, VendedorId, Guid.NewGuid()));

        anuncio.Fotos.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RemoverFotoAsync_ComAnuncioDeOutroVendedor_LancaAnuncioNaoPertenceAoVendedor()
    {
        var (_, service, anuncio) = await CenarioComAnuncio();
        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");
        var fotoId = anuncio.Fotos[0].Id;

        await Should.ThrowAsync<AnuncioNaoPertenceAoVendedorException>(() =>
            service.RemoverFotoAsync(anuncio.Id, OutroVendedorId, fotoId));
        anuncio.Fotos.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RemoverFotoAsync_ComIdDeAnuncioInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), new FakeBillingGateway(), new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() =>
            service.RemoverFotoAsync(Guid.NewGuid(), VendedorId, Guid.NewGuid()));
    }

    [Fact]
    public async Task DestacarAsync_ComCobrancaFalhando_LancaCobrancaFalhouExceptionENaoAlteraOAnuncio()
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        anuncio.Publicar();
        await repositorio.AdicionarAsync(anuncio);
        var billingGateway = new FakeBillingGateway(sucesso: false, motivoFalha: "Cartão recusado.");
        var service = new GerenciarAnuncioService(repositorio, new FakeMediaStorage(), billingGateway, new FakeVendedorRepository(), new FakePlanoLojistaRepository());

        var excecao = await Should.ThrowAsync<CobrancaFalhouException>(() => service.DestacarAsync(anuncio.Id, VendedorId));

        excecao.Message.ShouldBe("Cartão recusado.");
        anuncio.Patrocinado.ShouldBeFalse();
    }
}
