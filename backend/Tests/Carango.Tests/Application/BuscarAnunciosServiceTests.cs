using Carango.Application;
using Carango.Domain;
using Carango.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class BuscarAnunciosServiceTests
{
    private static Anuncio CriarAtivo(
        string marca = "Honda", string modelo = "Civic", int ano = 2019, string versao = "EXL",
        decimal preco = 95000m, string estado = "SP", string cidade = "São Paulo", string descricao = "Descrição qualquer")
    {
        var anuncio = Anuncio.CriarRascunho(
            Guid.NewGuid(), marca, modelo, ano, versao, preco, descricao, estado, cidade);
        anuncio.Publicar();
        return anuncio;
    }

    [Fact]
    public async Task BuscarAsync_SemFiltro_RetornaSoOsAtivos()
    {
        var repositorio = new FakeAnuncioRepository();
        var ativo = CriarAtivo();
        var rascunho = Anuncio.CriarRascunho(Guid.NewGuid(), marca: "Fiat", modelo: "Uno");
        var pausado = CriarAtivo(marca: "Ford", modelo: "Ka");
        pausado.Pausar();
        var vendido = CriarAtivo(marca: "Chevrolet", modelo: "Onix");
        vendido.MarcarComoVendido();
        await repositorio.AdicionarAsync(ativo);
        await repositorio.AdicionarAsync(rascunho);
        await repositorio.AdicionarAsync(pausado);
        await repositorio.AdicionarAsync(vendido);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca());

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(ativo);
    }

    [Fact]
    public async Task BuscarAsync_ComDoisOuMaisFiltros_AplicaSemanticaE()
    {
        var repositorio = new FakeAnuncioRepository();
        var combinaTudo = CriarAtivo(marca: "Honda", estado: "SP");
        var combinaSoMarca = CriarAtivo(marca: "Honda", estado: "RJ");
        var combinaSoEstado = CriarAtivo(marca: "Toyota", estado: "SP");
        await repositorio.AdicionarAsync(combinaTudo);
        await repositorio.AdicionarAsync(combinaSoMarca);
        await repositorio.AdicionarAsync(combinaSoEstado);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Marca: "Honda", Estado: "SP"));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(combinaTudo);
    }

    [Fact]
    public async Task BuscarAsync_SemNenhumResultado_RetornaListaVazia()
    {
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(marca: "Honda"));
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Marca: "Ferrari"));

        resultado.ShouldBeEmpty();
    }

    [Fact]
    public async Task BuscarAsync_ComFaixaDePreco_IncluiApenasDentroDosLimites()
    {
        var repositorio = new FakeAnuncioRepository();
        var barato = CriarAtivo(preco: 40000m);
        var noAlvo = CriarAtivo(preco: 60000m);
        var caro = CriarAtivo(preco: 90000m);
        await repositorio.AdicionarAsync(barato);
        await repositorio.AdicionarAsync(noAlvo);
        await repositorio.AdicionarAsync(caro);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(PrecoMin: 50000m, PrecoMax: 70000m));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(noAlvo);
    }

    [Fact]
    public async Task BuscarAsync_ComOrdenacaoPrecoAsc_OrdenaDoMaisBaratoAoMaisCaro()
    {
        var repositorio = new FakeAnuncioRepository();
        var caro = CriarAtivo(preco: 90000m);
        var barato = CriarAtivo(preco: 40000m);
        var medio = CriarAtivo(preco: 60000m);
        await repositorio.AdicionarAsync(caro);
        await repositorio.AdicionarAsync(barato);
        await repositorio.AdicionarAsync(medio);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Ordenacao: OrdenacaoBusca.PrecoAsc));

        resultado.Select(a => a.Preco).ShouldBe([40000m, 60000m, 90000m]);
    }

    [Fact]
    public async Task BuscarAsync_ComOrdenacaoPrecoDesc_OrdenaDoMaisCaroAoMaisBarato()
    {
        var repositorio = new FakeAnuncioRepository();
        var caro = CriarAtivo(preco: 90000m);
        var barato = CriarAtivo(preco: 40000m);
        var medio = CriarAtivo(preco: 60000m);
        await repositorio.AdicionarAsync(caro);
        await repositorio.AdicionarAsync(barato);
        await repositorio.AdicionarAsync(medio);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Ordenacao: OrdenacaoBusca.PrecoDesc));

        resultado.Select(a => a.Preco).ShouldBe([90000m, 60000m, 40000m]);
    }

    [Fact]
    public async Task BuscarAsync_ComOrdenacaoAnoAsc_OrdenaDoMaisAntigoAoMaisNovo()
    {
        var repositorio = new FakeAnuncioRepository();
        var novo = CriarAtivo(ano: 2022);
        var antigo = CriarAtivo(ano: 2015);
        var medio = CriarAtivo(ano: 2019);
        await repositorio.AdicionarAsync(novo);
        await repositorio.AdicionarAsync(antigo);
        await repositorio.AdicionarAsync(medio);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Ordenacao: OrdenacaoBusca.AnoAsc));

        resultado.Select(a => a.Ano).ShouldBe([2015, 2019, 2022]);
    }

    [Fact]
    public async Task BuscarAsync_ComOrdenacaoAnoDesc_OrdenaDoMaisNovoAoMaisAntigo()
    {
        var repositorio = new FakeAnuncioRepository();
        var novo = CriarAtivo(ano: 2022);
        var antigo = CriarAtivo(ano: 2015);
        var medio = CriarAtivo(ano: 2019);
        await repositorio.AdicionarAsync(novo);
        await repositorio.AdicionarAsync(antigo);
        await repositorio.AdicionarAsync(medio);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Ordenacao: OrdenacaoBusca.AnoDesc));

        resultado.Select(a => a.Ano).ShouldBe([2022, 2019, 2015]);
    }

    [Fact]
    public async Task BuscarAsync_ComOrdenacaoRelevanciaOuOmitida_ContinuaOrdenandoPorCriadoEmDecrescente()
    {
        var repositorio = new FakeAnuncioRepository();
        var primeiro = CriarAtivo(marca: "Honda");
        await repositorio.AdicionarAsync(primeiro);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var segundo = CriarAtivo(marca: "Toyota");
        await repositorio.AdicionarAsync(segundo);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Ordenacao: OrdenacaoBusca.Relevancia));

        resultado.Select(a => a.Id).ShouldBe([segundo.Id, primeiro.Id]);
    }

    [Fact]
    public async Task BuscarAsync_ComPrecoEmpatado_DesempataPorCriadoEmDecrescente()
    {
        var repositorio = new FakeAnuncioRepository();
        var maisAntigo = CriarAtivo(preco: 60000m);
        await repositorio.AdicionarAsync(maisAntigo);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var maisRecente = CriarAtivo(preco: 60000m);
        await repositorio.AdicionarAsync(maisRecente);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Ordenacao: OrdenacaoBusca.PrecoAsc));

        resultado.Select(a => a.Id).ShouldBe([maisRecente.Id, maisAntigo.Id]);
    }

    [Fact]
    public async Task BuscarAsync_ComTermoLivreQueCombinaComMarca_RetornaOAnuncio()
    {
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(marca: "Honda");
        var naoCombina = CriarAtivo(marca: "Toyota");
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(TermoLivre: "Honda"));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(combina);
    }

    [Fact]
    public async Task BuscarAsync_ComTermoLivreQueCombinaComModelo_RetornaOAnuncio()
    {
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(modelo: "Civic");
        var naoCombina = CriarAtivo(modelo: "Corolla");
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(TermoLivre: "Civic"));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(combina);
    }

    [Fact]
    public async Task BuscarAsync_ComTermoLivreQueCombinaComVersao_RetornaOAnuncio()
    {
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(versao: "EXL");
        var naoCombina = CriarAtivo(versao: "LX");
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(TermoLivre: "EXL"));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(combina);
    }

    [Fact]
    public async Task BuscarAsync_ComTermoLivreQueCombinaComDescricao_RetornaOAnuncio()
    {
        var repositorio = new FakeAnuncioRepository();
        var combina = CriarAtivo(descricao: "Único dono, revisado na concessionária");
        var naoCombina = CriarAtivo(descricao: "Carro de garagem");
        await repositorio.AdicionarAsync(combina);
        await repositorio.AdicionarAsync(naoCombina);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(TermoLivre: "concessionária"));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(combina);
    }

    [Fact]
    public async Task BuscarAsync_ComTermoLivreDeDuasPalavras_ExigeAsDuasEmQualquerCampo()
    {
        var repositorio = new FakeAnuncioRepository();
        var combinaTudo = CriarAtivo(modelo: "Civic", descricao: "Modelo 2019, único dono");
        var combinaSoModelo = CriarAtivo(modelo: "Civic", descricao: "Carro de garagem");
        var combinaSoAno = CriarAtivo(modelo: "Corolla", descricao: "Modelo 2019, único dono");
        await repositorio.AdicionarAsync(combinaTudo);
        await repositorio.AdicionarAsync(combinaSoModelo);
        await repositorio.AdicionarAsync(combinaSoAno);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(TermoLivre: "Civic 2019"));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(combinaTudo);
    }

    [Fact]
    public async Task BuscarAsync_ComTermoLivreSemNenhumResultado_RetornaListaVazia()
    {
        var repositorio = new FakeAnuncioRepository();
        await repositorio.AdicionarAsync(CriarAtivo(marca: "Honda"));
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(TermoLivre: "Ferrari"));

        resultado.ShouldBeEmpty();
    }

    [Fact]
    public async Task BuscarAsync_ComTermoLivreEFiltroEstruturado_AplicaOsDoisComSemanticaE()
    {
        var repositorio = new FakeAnuncioRepository();
        var combinaTudo = CriarAtivo(modelo: "Civic", estado: "SP");
        var combinaSoTermo = CriarAtivo(modelo: "Civic", estado: "RJ");
        var combinaSoEstado = CriarAtivo(modelo: "Corolla", estado: "SP");
        await repositorio.AdicionarAsync(combinaTudo);
        await repositorio.AdicionarAsync(combinaSoTermo);
        await repositorio.AdicionarAsync(combinaSoEstado);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(Estado: "SP", TermoLivre: "Civic"));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(combinaTudo);
    }

    [Fact]
    public async Task BuscarAsync_ComTermoLivreSeparadoPorTabulacao_DivideOsTokensCorretamente()
    {
        var repositorio = new FakeAnuncioRepository();
        var combinaTudo = CriarAtivo(modelo: "Civic", descricao: "Modelo 2019, único dono");
        var combinaSoModelo = CriarAtivo(modelo: "Civic", descricao: "Carro de garagem");
        await repositorio.AdicionarAsync(combinaTudo);
        await repositorio.AdicionarAsync(combinaSoModelo);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.BuscarAsync(new FiltroBusca(TermoLivre: "Civic\t2019"));

        resultado.ShouldHaveSingleItem();
        resultado[0].ShouldBe(combinaTudo);
    }

    [Fact]
    public async Task ObterDetalhePublicoAsync_ComAnuncioAtivo_RetornaOAnuncio()
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = CriarAtivo();
        await repositorio.AdicionarAsync(anuncio);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.ObterDetalhePublicoAsync(anuncio.Id);

        resultado.ShouldBe(anuncio);
    }

    [Fact]
    public async Task ObterDetalhePublicoAsync_ComIdInexistente_LancaAnuncioNaoEncontrado()
    {
        var repositorio = new FakeAnuncioRepository();
        var service = new BuscarAnunciosService(repositorio);

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.ObterDetalhePublicoAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("rascunho")]
    [InlineData("pausado")]
    [InlineData("vendido")]
    public async Task ObterDetalhePublicoAsync_ComAnuncioNaoAtivo_LancaAnuncioNaoEncontrado(string status)
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = Anuncio.CriarRascunho(
            Guid.NewGuid(), "Honda", "Civic", 2019, "EXL", 95000m, "Descrição qualquer", "SP", "São Paulo");
        if (status != "rascunho")
        {
            anuncio.Publicar();
            if (status == "pausado") anuncio.Pausar();
            if (status == "vendido") anuncio.MarcarComoVendido();
        }
        await repositorio.AdicionarAsync(anuncio);
        var service = new BuscarAnunciosService(repositorio);

        await Should.ThrowAsync<AnuncioNaoEncontradoException>(() => service.ObterDetalhePublicoAsync(anuncio.Id));
    }

    [Fact]
    public async Task ObterDetalhePublicoAsync_ComAnuncioAtivo_RegistraUmaVisualizacao()
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = CriarAtivo();
        await repositorio.AdicionarAsync(anuncio);
        var service = new BuscarAnunciosService(repositorio);

        await service.ObterDetalhePublicoAsync(anuncio.Id);

        anuncio.Visualizacoes.ShouldBe(1);
    }

    [Fact]
    public async Task ObterDetalhePublicoAsync_ChamadoDuasVezes_RegistraDuasVisualizacoes()
    {
        var repositorio = new FakeAnuncioRepository();
        var anuncio = CriarAtivo();
        await repositorio.AdicionarAsync(anuncio);
        var service = new BuscarAnunciosService(repositorio);

        await service.ObterDetalhePublicoAsync(anuncio.Id);
        await service.ObterDetalhePublicoAsync(anuncio.Id);

        anuncio.Visualizacoes.ShouldBe(2);
    }

    [Fact]
    public async Task ObterDetalhePublicoAsync_QuandoIncrementarVisualizacaoFalha_AindaAssimRetornaODetalhe()
    {
        var repositorio = new RepositorioQueFalhaAoIncrementarVisualizacao(new FakeAnuncioRepository());
        var anuncio = CriarAtivo();
        await repositorio.AdicionarAsync(anuncio);
        var service = new BuscarAnunciosService(repositorio);

        var resultado = await service.ObterDetalhePublicoAsync(anuncio.Id);

        resultado.ShouldBe(anuncio);
    }

    // decorator só pra este teste — melhor esforço (Story 4.5): uma falha ao registrar a
    // visualização não pode quebrar o Detalhe público, que é o que este teste comprova
    private class RepositorioQueFalhaAoIncrementarVisualizacao(IAnuncioRepository interno) : IAnuncioRepository
    {
        public Task AdicionarAsync(Anuncio anuncio) => interno.AdicionarAsync(anuncio);
        public Task<int> ContarAtivosPorVendedorAsync(Guid vendedorId) => interno.ContarAtivosPorVendedorAsync(vendedorId);
        public Task<Anuncio?> ObterPorIdAsync(Guid id) => interno.ObterPorIdAsync(id);
        public Task AtualizarAsync(Anuncio anuncio) => interno.AtualizarAsync(anuncio);
        public Task ExcluirAsync(Anuncio anuncio) => interno.ExcluirAsync(anuncio);
        public Task<IReadOnlyList<Anuncio>> ListarPorVendedorAsync(Guid vendedorId) => interno.ListarPorVendedorAsync(vendedorId);
        public Task<IReadOnlyList<Anuncio>> BuscarAsync(FiltroBusca filtro) => interno.BuscarAsync(filtro);
        public Task<Anuncio?> ObterAtivoPorIdAsync(Guid id) => interno.ObterAtivoPorIdAsync(id);
        public Task IncrementarVisualizacaoAsync(Guid id) => throw new InvalidOperationException("Falha simulada");
    }
}
