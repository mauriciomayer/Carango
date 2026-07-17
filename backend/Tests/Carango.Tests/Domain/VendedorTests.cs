using Carango.Domain;
using Shouldly;
using Xunit;

namespace Carango.Tests.Domain;

public class VendedorTests
{
    [Fact]
    public void Construtor_ComTipoPessoaFisica_NaoExigeCnpjRazaoSocial()
    {
        var vendedor = new Vendedor("marcos@exemplo.com", "hash-fake", TipoVendedor.PessoaFisica);

        vendedor.Id.ShouldNotBe(Guid.Empty);
        vendedor.Tipo.ShouldBe(TipoVendedor.PessoaFisica);
        vendedor.CnpjRazaoSocial.ShouldBeNull();
    }

    [Fact]
    public void Construtor_ComTipoLojistaSemCnpjRazaoSocial_LancaArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            new Vendedor("loja@exemplo.com", "hash-fake", TipoVendedor.Lojista));
    }

    [Fact]
    public void Construtor_ComTipoLojistaComCnpjRazaoSocial_CriaVendedor()
    {
        var vendedor = new Vendedor("loja@exemplo.com", "hash-fake", TipoVendedor.Lojista, cnpjRazaoSocial: "12.345.678/0001-90 - Auto Center");

        vendedor.Tipo.ShouldBe(TipoVendedor.Lojista);
        vendedor.CnpjRazaoSocial.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Construtor_ComEmailVazio_LancaArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            new Vendedor("", "hash-fake", TipoVendedor.PessoaFisica));
    }

    [Fact]
    public void Construtor_ComSenhaHashVazia_LancaArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            new Vendedor("marcos@exemplo.com", "", TipoVendedor.PessoaFisica));
    }

    [Fact]
    public void Construtor_ComTipoForaDoVocabularioFechado_LancaArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            new Vendedor("marcos@exemplo.com", "hash-fake", (TipoVendedor)99));
    }
}
