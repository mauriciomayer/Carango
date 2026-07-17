using Carango.Domain;
using Shouldly;
using Xunit;

namespace Carango.Tests.Domain;

public class AnuncioTests
{
    private static readonly Guid VendedorId = Guid.NewGuid();

    [Fact]
    public void CriarRascunho_SemNenhumCampo_NaoValidaNadaEFicaComoRascunho()
    {
        var anuncio = Anuncio.CriarRascunho(VendedorId);

        anuncio.Id.ShouldNotBe(Guid.Empty);
        anuncio.Status.ShouldBe(StatusAnuncio.Rascunho);
        anuncio.Marca.ShouldBeNull();
        anuncio.Fotos.ShouldBeEmpty();
    }

    [Fact]
    public void CriarRascunho_ComVendedorIdVazio_LancaArgumentException()
    {
        Should.Throw<ArgumentException>(() => Anuncio.CriarRascunho(Guid.Empty));
    }

    [Fact]
    public void CriarRascunho_PopulaCriadoEmComDataAtualUtc()
    {
        var antes = DateTime.UtcNow;

        var anuncio = Anuncio.CriarRascunho(VendedorId);

        var depois = DateTime.UtcNow;
        anuncio.CriadoEm.ShouldBeInRange(antes.AddSeconds(-1), depois.AddSeconds(1));
    }

    [Fact]
    public void Publicar_ComTodosOsCamposPreenchidos_TransicionaParaAtivo()
    {
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");

        anuncio.Publicar();

        anuncio.Status.ShouldBe(StatusAnuncio.Ativo);
    }

    [Theory]
    [InlineData(null, "Civic", 2019, "EXL", 95000d,"desc", "SP", "São Paulo")]
    [InlineData("Honda", null, 2019, "EXL", 95000d,"desc", "SP", "São Paulo")]
    [InlineData("Honda", "Civic", null, "EXL", 95000d,"desc", "SP", "São Paulo")]
    [InlineData("Honda", "Civic", 2019, null, 95000d,"desc", "SP", "São Paulo")]
    [InlineData("Honda", "Civic", 2019, "EXL", null, "desc", "SP", "São Paulo")]
    [InlineData("Honda", "Civic", 2019, "EXL", 95000d,null, "SP", "São Paulo")]
    [InlineData("Honda", "Civic", 2019, "EXL", 95000d,"desc", null, "São Paulo")]
    [InlineData("Honda", "Civic", 2019, "EXL", 95000d,"desc", "SP", null)]
    public void Publicar_ComAlgumCampoObrigatorioAusente_LancaArgumentExceptionEPermaneceRascunho(
        string? marca, string? modelo, int? ano, string? versao, double? preco, string? descricao, string? estado, string? cidade)
    {
        // decimal? não é um tipo válido de argumento de atributo em C# (xUnit [InlineData]) — recebido como
        // double? e convertido aqui só pra viabilizar os casos de teste parametrizados
        var anuncio = Anuncio.CriarRascunho(VendedorId, marca, modelo, ano, versao, (decimal?)preco, descricao, estado, cidade);

        Should.Throw<ArgumentException>(() => anuncio.Publicar());

        anuncio.Status.ShouldBe(StatusAnuncio.Rascunho);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-100d)]
    public void Publicar_ComPrecoZeroOuNegativo_LancaArgumentExceptionEPermaneceRascunho(double preco)
    {
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: (decimal)preco, descricao: "Único dono", estado: "SP", cidade: "São Paulo");

        Should.Throw<ArgumentException>(() => anuncio.Publicar());

        anuncio.Status.ShouldBe(StatusAnuncio.Rascunho);
    }

    [Fact]
    public void Publicar_QuandoJaEstaAtivo_LancaInvalidOperationException()
    {
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        anuncio.Publicar();

        Should.Throw<InvalidOperationException>(() => anuncio.Publicar());
    }

    [Fact]
    public void AtualizarFicha_EmRascunho_AceitaQualquerCombinacaoInclusiveTudoEmBranco()
    {
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");

        anuncio.AtualizarFicha(null, null, null, null, null, null, null, null);

        anuncio.Status.ShouldBe(StatusAnuncio.Rascunho);
        anuncio.Marca.ShouldBeNull();
        anuncio.Preco.ShouldBeNull();
    }

    [Fact]
    public void AtualizarFicha_EmAtivoComTodosOsCamposValidos_AplicaAsMudancas()
    {
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        anuncio.Publicar();

        anuncio.AtualizarFicha("Toyota", "Corolla", 2021, "XEI", 110000m, "Revisado", "RJ", "Rio de Janeiro");

        anuncio.Status.ShouldBe(StatusAnuncio.Ativo);
        anuncio.Marca.ShouldBe("Toyota");
        anuncio.Modelo.ShouldBe("Corolla");
        anuncio.Preco.ShouldBe(110000m);
        anuncio.Cidade.ShouldBe("Rio de Janeiro");
    }

    [Fact]
    public void AtualizarFicha_EmAtivoComCampoObrigatorioFaltando_LancaArgumentExceptionENaoAlteraNada()
    {
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        anuncio.Publicar();

        Should.Throw<ArgumentException>(() =>
            anuncio.AtualizarFicha(null, "Corolla", 2021, "XEI", 110000m, "Revisado", "RJ", "Rio de Janeiro"));

        anuncio.Marca.ShouldBe("Honda");
        anuncio.Modelo.ShouldBe("Civic");
        anuncio.Status.ShouldBe(StatusAnuncio.Ativo);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-50d)]
    public void AtualizarFicha_EmAtivoComPrecoZeroOuNegativo_LancaArgumentExceptionENaoAlteraNada(double preco)
    {
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        anuncio.Publicar();

        Should.Throw<ArgumentException>(() =>
            anuncio.AtualizarFicha("Toyota", "Corolla", 2021, "XEI", (decimal)preco, "Revisado", "RJ", "Rio de Janeiro"));

        anuncio.Marca.ShouldBe("Honda");
        anuncio.Preco.ShouldBe(95000m);
    }

    private static Anuncio CriarAtivo()
    {
        var anuncio = Anuncio.CriarRascunho(
            VendedorId, marca: "Honda", modelo: "Civic", ano: 2019, versao: "EXL",
            preco: 95000m, descricao: "Único dono", estado: "SP", cidade: "São Paulo");
        anuncio.Publicar();
        return anuncio;
    }

    private static Anuncio CriarPausado()
    {
        var anuncio = CriarAtivo();
        anuncio.Pausar();
        return anuncio;
    }

    [Fact]
    public void Pausar_DeAtivo_TransicionaParaPausado()
    {
        var anuncio = CriarAtivo();

        anuncio.Pausar();

        anuncio.Status.ShouldBe(StatusAnuncio.Pausado);
    }

    [Theory]
    [InlineData(StatusAnuncio.Rascunho)]
    [InlineData(StatusAnuncio.Pausado)]
    [InlineData(StatusAnuncio.Vendido)]
    public void Pausar_ForaDeAtivo_LancaInvalidOperationException(StatusAnuncio statusAtual)
    {
        var anuncio = statusAtual switch
        {
            StatusAnuncio.Rascunho => Anuncio.CriarRascunho(VendedorId),
            StatusAnuncio.Pausado => CriarPausado(),
            StatusAnuncio.Vendido => CriarAtivo(),
            _ => throw new ArgumentOutOfRangeException(nameof(statusAtual)),
        };
        if (statusAtual == StatusAnuncio.Vendido) anuncio.MarcarComoVendido();

        Should.Throw<InvalidOperationException>(() => anuncio.Pausar());
    }

    [Fact]
    public void Reativar_DePausado_TransicionaParaAtivo()
    {
        var anuncio = CriarPausado();

        anuncio.Reativar();

        anuncio.Status.ShouldBe(StatusAnuncio.Ativo);
    }

    [Fact]
    public void Reativar_DePausadoComCampoObrigatorioAusente_LancaArgumentExceptionEPermaneceEmPausado()
    {
        // regressão: AtualizarFicha só valida quando Status == Ativo, então um Anúncio Pausado podia
        // ser editado com campos em branco; Reativar() precisa da mesma garantia de Publicar() —
        // nenhum Anúncio Ativo pode ficar com a Ficha incompleta
        var anuncio = CriarPausado();
        anuncio.AtualizarFicha(null, null, null, null, null, null, null, null);

        Should.Throw<ArgumentException>(() => anuncio.Reativar());

        anuncio.Status.ShouldBe(StatusAnuncio.Pausado);
    }

    [Theory]
    [InlineData(StatusAnuncio.Rascunho)]
    [InlineData(StatusAnuncio.Ativo)]
    [InlineData(StatusAnuncio.Vendido)]
    public void Reativar_ForaDePausado_LancaInvalidOperationException(StatusAnuncio statusAtual)
    {
        var anuncio = statusAtual switch
        {
            StatusAnuncio.Rascunho => Anuncio.CriarRascunho(VendedorId),
            StatusAnuncio.Ativo => CriarAtivo(),
            StatusAnuncio.Vendido => CriarAtivo(),
            _ => throw new ArgumentOutOfRangeException(nameof(statusAtual)),
        };
        if (statusAtual == StatusAnuncio.Vendido) anuncio.MarcarComoVendido();

        Should.Throw<InvalidOperationException>(() => anuncio.Reativar());
    }

    [Fact]
    public void MarcarComoVendido_DeAtivo_TransicionaParaVendido()
    {
        var anuncio = CriarAtivo();

        anuncio.MarcarComoVendido();

        anuncio.Status.ShouldBe(StatusAnuncio.Vendido);
    }

    [Fact]
    public void MarcarComoVendido_DePausado_TransicionaParaVendido()
    {
        var anuncio = CriarPausado();

        anuncio.MarcarComoVendido();

        anuncio.Status.ShouldBe(StatusAnuncio.Vendido);
    }

    [Theory]
    [InlineData(StatusAnuncio.Rascunho)]
    [InlineData(StatusAnuncio.Vendido)]
    public void MarcarComoVendido_ForaDeAtivoOuPausado_LancaInvalidOperationException(StatusAnuncio statusAtual)
    {
        var anuncio = statusAtual switch
        {
            StatusAnuncio.Rascunho => Anuncio.CriarRascunho(VendedorId),
            StatusAnuncio.Vendido => CriarAtivo(),
            _ => throw new ArgumentOutOfRangeException(nameof(statusAtual)),
        };
        if (statusAtual == StatusAnuncio.Vendido) anuncio.MarcarComoVendido();

        Should.Throw<InvalidOperationException>(() => anuncio.MarcarComoVendido());
    }

    [Fact]
    public void Destacar_DeAtivo_MarcaComoPatrocinado()
    {
        var anuncio = CriarAtivo();

        anuncio.Destacar();

        anuncio.Patrocinado.ShouldBeTrue();
    }

    [Theory]
    [InlineData(StatusAnuncio.Rascunho)]
    [InlineData(StatusAnuncio.Pausado)]
    [InlineData(StatusAnuncio.Vendido)]
    public void Destacar_ForaDeAtivo_LancaInvalidOperationException(StatusAnuncio statusAtual)
    {
        var anuncio = statusAtual switch
        {
            StatusAnuncio.Rascunho => Anuncio.CriarRascunho(VendedorId),
            StatusAnuncio.Pausado => CriarPausado(),
            StatusAnuncio.Vendido => CriarAtivo(),
            _ => throw new ArgumentOutOfRangeException(nameof(statusAtual)),
        };
        if (statusAtual == StatusAnuncio.Vendido) anuncio.MarcarComoVendido();

        Should.Throw<InvalidOperationException>(() => anuncio.Destacar());
        anuncio.Patrocinado.ShouldBeFalse();
    }

    [Fact]
    public void RemoverDestaque_QuandoJaNaoEstaPatrocinado_NaoLancaNada()
    {
        var anuncio = CriarAtivo();

        Should.NotThrow(() => anuncio.RemoverDestaque());
        anuncio.Patrocinado.ShouldBeFalse();
    }

    [Fact]
    public void Pausar_ComAnuncioPatrocinado_RemoveODestaqueJuntoComATransicao()
    {
        var anuncio = CriarAtivo();
        anuncio.Destacar();

        anuncio.Pausar();

        anuncio.Status.ShouldBe(StatusAnuncio.Pausado);
        anuncio.Patrocinado.ShouldBeFalse();
    }

    [Fact]
    public void MarcarComoVendido_ComAnuncioPatrocinado_RemoveODestaqueJuntoComATransicao()
    {
        var anuncio = CriarAtivo();
        anuncio.Destacar();

        anuncio.MarcarComoVendido();

        anuncio.Status.ShouldBe(StatusAnuncio.Vendido);
        anuncio.Patrocinado.ShouldBeFalse();
    }

    [Fact]
    public void AdicionarFoto_ComMultiplasFotos_PreservaOrdemDeInsercao()
    {
        var anuncio = Anuncio.CriarRascunho(VendedorId);

        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");
        anuncio.AdicionarFoto("/uploads/anuncios/foto2.jpg");
        anuncio.AdicionarFoto("/uploads/anuncios/foto3.jpg");

        anuncio.Fotos.Count.ShouldBe(3);
        anuncio.Fotos[0].Url.ShouldBe("/uploads/anuncios/foto1.jpg");
        anuncio.Fotos[0].Ordem.ShouldBe(0);
        anuncio.Fotos[2].Url.ShouldBe("/uploads/anuncios/foto3.jpg");
        anuncio.Fotos[2].Ordem.ShouldBe(2);
    }

    [Fact]
    public void RemoverFoto_ComIdExistente_RemoveDaListaERetornaAUrl()
    {
        var anuncio = Anuncio.CriarRascunho(VendedorId);
        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");
        anuncio.AdicionarFoto("/uploads/anuncios/foto2.jpg");
        var idParaRemover = anuncio.Fotos[0].Id;

        var urlRemovida = anuncio.RemoverFoto(idParaRemover);

        urlRemovida.ShouldBe("/uploads/anuncios/foto1.jpg");
        anuncio.Fotos.Count.ShouldBe(1);
        anuncio.Fotos[0].Url.ShouldBe("/uploads/anuncios/foto2.jpg");
    }

    [Fact]
    public void AdicionarFoto_DepoisDeRemoverUmaDoMeio_NaoColideOrdemComAsRestantes()
    {
        // regressão achada no code review: Ordem = _fotos.Count só era seguro enquanto a lista só
        // crescia; remover uma foto do meio e adicionar outra depois colidia o Ordem da nova com
        // o de uma foto que já existia (3 fotos Ordem 0/1/2, remove a de Ordem 0, adiciona uma
        // nova → Count=2 colidia com a foto que já tinha Ordem=2)
        var anuncio = Anuncio.CriarRascunho(VendedorId);
        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");
        anuncio.AdicionarFoto("/uploads/anuncios/foto2.jpg");
        anuncio.AdicionarFoto("/uploads/anuncios/foto3.jpg");
        var idDaPrimeira = anuncio.Fotos[0].Id;
        anuncio.RemoverFoto(idDaPrimeira);

        anuncio.AdicionarFoto("/uploads/anuncios/foto4.jpg");

        var ordens = anuncio.Fotos.Select(f => f.Ordem).ToList();
        ordens.ShouldBe(ordens.Distinct());
        anuncio.Fotos.Last().Url.ShouldBe("/uploads/anuncios/foto4.jpg");
    }

    [Fact]
    public void RemoverFoto_ComIdInexistente_RetornaNullENaoAlteraNada()
    {
        var anuncio = Anuncio.CriarRascunho(VendedorId);
        anuncio.AdicionarFoto("/uploads/anuncios/foto1.jpg");

        var resultado = anuncio.RemoverFoto(Guid.NewGuid());

        resultado.ShouldBeNull();
        anuncio.Fotos.Count.ShouldBe(1);
    }

    [Fact]
    public void CriarRascunho_Sempre_ComecaComVisualizacoesZerado()
    {
        var anuncio = Anuncio.CriarRascunho(VendedorId);

        anuncio.Visualizacoes.ShouldBe(0);
    }

    [Fact]
    public void RegistrarVisualizacao_ChamadoUmaVez_IncrementaDeZeroParaUm()
    {
        var anuncio = CriarAtivo();

        anuncio.RegistrarVisualizacao();

        anuncio.Visualizacoes.ShouldBe(1);
    }

    [Fact]
    public void RegistrarVisualizacao_ChamadoTresVezes_ChegaATres()
    {
        var anuncio = CriarAtivo();

        anuncio.RegistrarVisualizacao();
        anuncio.RegistrarVisualizacao();
        anuncio.RegistrarVisualizacao();

        anuncio.Visualizacoes.ShouldBe(3);
    }
}
