using Carango.Application;
using Carango.Domain;
using Carango.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class CadastrarVendedorServiceTests
{
    private static CadastrarVendedorService CriarService(FakeVendedorRepository repositorio, FakePasswordHasher? hasher = null)
        => new(repositorio, hasher ?? new FakePasswordHasher());

    [Fact]
    public async Task CadastrarAsync_ComEmailNovo_PersisteVendedorComSenhaHasheada()
    {
        var repositorio = new FakeVendedorRepository();
        var service = CriarService(repositorio);
        var input = new CadastrarVendedorInput("marcos@exemplo.com", "senha-secreta", TipoVendedor.PessoaFisica);

        var vendedor = await service.CadastrarAsync(input);

        repositorio.Vendedores.ShouldContain(vendedor);
        vendedor.SenhaHash.ShouldNotBe("senha-secreta");
        vendedor.SenhaHash.ShouldBe(new FakePasswordHasher().Hash("senha-secreta"));
    }

    [Fact]
    public async Task CadastrarAsync_ComEmailJaCadastrado_LancaEmailJaCadastradoExceptionSemPersistirDeNovo()
    {
        var repositorio = new FakeVendedorRepository();
        var service = CriarService(repositorio);
        var input = new CadastrarVendedorInput("marcos@exemplo.com", "senha-secreta", TipoVendedor.PessoaFisica);
        await service.CadastrarAsync(input);

        await Should.ThrowAsync<EmailJaCadastradoException>(() => service.CadastrarAsync(input));

        repositorio.Vendedores.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CadastrarAsync_LojistaSemCnpjRazaoSocial_LancaArgumentException()
    {
        var repositorio = new FakeVendedorRepository();
        var service = CriarService(repositorio);
        var input = new CadastrarVendedorInput("loja@exemplo.com", "senha-secreta", TipoVendedor.Lojista);

        await Should.ThrowAsync<ArgumentException>(() => service.CadastrarAsync(input));

        repositorio.Vendedores.ShouldBeEmpty();
    }

    [Fact]
    public async Task CadastrarAsync_Lojista_ComCnpjRazaoSocial_PersisteVendedor()
    {
        var repositorio = new FakeVendedorRepository();
        var service = CriarService(repositorio);
        var input = new CadastrarVendedorInput("loja@exemplo.com", "senha-secreta", TipoVendedor.Lojista, CnpjRazaoSocial: "12.345.678/0001-90");

        var vendedor = await service.CadastrarAsync(input);

        vendedor.CnpjRazaoSocial.ShouldBe("12.345.678/0001-90");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CadastrarAsync_ComSenhaVaziaOuEmBranco_LancaArgumentExceptionSemPersistir(string senha)
    {
        var repositorio = new FakeVendedorRepository();
        var service = CriarService(repositorio);
        var input = new CadastrarVendedorInput("marcos@exemplo.com", senha, TipoVendedor.PessoaFisica);

        await Should.ThrowAsync<ArgumentException>(() => service.CadastrarAsync(input));

        repositorio.Vendedores.ShouldBeEmpty();
    }

    [Fact]
    public async Task CadastrarAsync_ComEmailComEspacosEmVolta_NormalizaAntesDeChecarDuplicidadeEPersistir()
    {
        var repositorio = new FakeVendedorRepository();
        var service = CriarService(repositorio);
        await service.CadastrarAsync(new CadastrarVendedorInput("marcos@exemplo.com", "senha-secreta", TipoVendedor.PessoaFisica));

        var comEspacos = new CadastrarVendedorInput(" marcos@exemplo.com ", "outra-senha", TipoVendedor.PessoaFisica);
        await Should.ThrowAsync<EmailJaCadastradoException>(() => service.CadastrarAsync(comEspacos));

        repositorio.Vendedores.Count.ShouldBe(1);
        repositorio.Vendedores.Single().Email.ShouldBe("marcos@exemplo.com");
    }
}
