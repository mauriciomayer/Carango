using Carango.Application;
using Carango.Domain;
using Carango.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class CriarAnuncioServiceTests
{
    private static readonly Guid VendedorId = Guid.NewGuid();

    private static CriarAnuncioInput InputCompleto(bool publicar, TipoVendedor tipo = TipoVendedor.PessoaFisica, IReadOnlyList<ArquivoFoto>? fotos = null) =>
        new(VendedorId, tipo, publicar, "Honda", "Civic", 2019, "EXL", 95000m, "Único dono", "SP", "São Paulo", fotos ?? []);

    [Fact]
    public async Task CriarAsync_ComPublicarTrueSemAnuncioAtivoExistente_CriaComoAtivo()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new CriarAnuncioService(repositorio, new FakeMediaStorage(), new FakePlanoLojistaRepository());

        var anuncio = await service.CriarAsync(InputCompleto(publicar: true));

        anuncio.Status.ShouldBe(StatusAnuncio.Ativo);
        repositorio.Anuncios.ShouldContain(anuncio);
    }

    [Fact]
    public async Task CriarAsync_ComPublicarTrueEUmAtivoExistente_PessoaFisica_LancaLimiteExcedidoSemPersistir()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new CriarAnuncioService(repositorio, new FakeMediaStorage(), new FakePlanoLojistaRepository());
        await service.CriarAsync(InputCompleto(publicar: true));

        await Should.ThrowAsync<LimiteDeAnunciosAtivosExcedidoException>(() => service.CriarAsync(InputCompleto(publicar: true)));

        repositorio.Anuncios.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CriarAsync_ComPublicarFalseEUmAtivoExistente_SalvaRascunhoSemContarNoLimite()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new CriarAnuncioService(repositorio, new FakeMediaStorage(), new FakePlanoLojistaRepository());
        await service.CriarAsync(InputCompleto(publicar: true));

        var rascunho = await service.CriarAsync(InputCompleto(publicar: false));

        rascunho.Status.ShouldBe(StatusAnuncio.Rascunho);
        repositorio.Anuncios.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CriarAsync_ComCampoObrigatorioAusenteEPublicarTrue_LancaArgumentExceptionSemPersistir()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new CriarAnuncioService(repositorio, new FakeMediaStorage(), new FakePlanoLojistaRepository());
        var input = new CriarAnuncioInput(VendedorId, TipoVendedor.PessoaFisica, Publicar: true,
            Marca: null, Modelo: "Civic", Ano: 2019, Versao: "EXL", Preco: 95000m,
            Descricao: "desc", Estado: "SP", Cidade: "São Paulo", Fotos: []);

        await Should.ThrowAsync<ArgumentException>(() => service.CriarAsync(input));

        repositorio.Anuncios.ShouldBeEmpty();
    }

    [Fact]
    public async Task CriarAsync_ComMultiplasFotos_TodasPersistidasNaOrdemEnviada()
    {
        var repositorio = new FakeAnuncioRepository();
        var mediaStorage = new FakeMediaStorage();
        var service = new CriarAnuncioService(repositorio, mediaStorage, new FakePlanoLojistaRepository());
        var fotos = new List<ArquivoFoto>
        {
            new(new MemoryStream([1, 2, 3]), "foto1.jpg", "image/jpeg"),
            new(new MemoryStream([4, 5, 6]), "foto2.jpg", "image/jpeg"),
            new(new MemoryStream([7, 8, 9]), "foto3.jpg", "image/jpeg"),
        };

        var anuncio = await service.CriarAsync(InputCompleto(publicar: true, fotos: fotos));

        anuncio.Fotos.Count.ShouldBe(3);
        anuncio.Fotos[0].Url.ShouldContain("foto1.jpg");
        anuncio.Fotos[1].Url.ShouldContain("foto2.jpg");
        anuncio.Fotos[2].Url.ShouldContain("foto3.jpg");
        anuncio.Fotos[0].Ordem.ShouldBe(0);
        anuncio.Fotos[2].Ordem.ShouldBe(2);
        mediaStorage.ArquivosSalvos.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CriarAsync_LojistaSemPlanoAtivo_ContinuaBloqueadoComoPessoaFisica()
    {
        // Story 4.2, AC #2: Lojista sem Plano Lojista ativo permanece limitado ao padrão de
        // Pessoa Física — regressão explícita pra não quebrar a Story 2.1
        var repositorio = new FakeAnuncioRepository();
        var service = new CriarAnuncioService(repositorio, new FakeMediaStorage(), new FakePlanoLojistaRepository());
        await service.CriarAsync(InputCompleto(publicar: true, tipo: TipoVendedor.Lojista));

        await Should.ThrowAsync<LimiteDeAnunciosAtivosExcedidoException>(() =>
            service.CriarAsync(InputCompleto(publicar: true, tipo: TipoVendedor.Lojista)));

        repositorio.Anuncios.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CriarAsync_LojistaComPlanoAtivo_PublicaSegundoAnuncioAtivoComSucesso()
    {
        // Story 4.2, AC #1: Plano Lojista ativo isenta o limite de 1 Anúncio ativo
        var repositorio = new FakeAnuncioRepository();
        var planoRepositorio = new FakePlanoLojistaRepository();
        var service = new CriarAnuncioService(repositorio, new FakeMediaStorage(), planoRepositorio);
        await service.CriarAsync(InputCompleto(publicar: true, tipo: TipoVendedor.Lojista));
        planoRepositorio.Planos.Add(PlanoLojista.Assinar(VendedorId));

        var segundo = await service.CriarAsync(InputCompleto(publicar: true, tipo: TipoVendedor.Lojista));

        segundo.Status.ShouldBe(StatusAnuncio.Ativo);
        repositorio.Anuncios.Count.ShouldBe(2);
    }
}
