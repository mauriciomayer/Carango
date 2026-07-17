using Carango.Application;
using Carango.Domain;
using Carango.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class AutenticarVendedorServiceTests
{
    private static async Task<(FakeVendedorRepository Repositorio, FakePasswordHasher Hasher, AutenticarVendedorService Service)> CriarCenarioComVendedorCadastrado(
        string email = "marcos@exemplo.com", string senha = "senha-secreta")
    {
        var repositorio = new FakeVendedorRepository();
        var hasher = new FakePasswordHasher();
        var cadastro = new CadastrarVendedorService(repositorio, hasher);
        await cadastro.CadastrarAsync(new CadastrarVendedorInput(email, senha, TipoVendedor.PessoaFisica));

        var service = new AutenticarVendedorService(repositorio, hasher, new FakeGeradorToken());
        return (repositorio, hasher, service);
    }

    [Fact]
    public async Task AutenticarAsync_ComCredenciaisCorretas_RetornaVendedorETokenGerado()
    {
        var (_, _, service) = await CriarCenarioComVendedorCadastrado(senha: "senha-secreta");

        var (vendedor, token) = await service.AutenticarAsync(new AutenticarVendedorInput("marcos@exemplo.com", "senha-secreta"));

        vendedor.Email.ShouldBe("marcos@exemplo.com");
        token.Token.ShouldNotBeNullOrWhiteSpace();
        token.ExpiraEmUtc.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public async Task AutenticarAsync_ComEmailNaoCadastrado_LancaCredenciaisInvalidasException()
    {
        var (_, _, service) = await CriarCenarioComVendedorCadastrado();

        await Should.ThrowAsync<CredenciaisInvalidasException>(() =>
            service.AutenticarAsync(new AutenticarVendedorInput("desconhecido@exemplo.com", "qualquer-senha")));
    }

    [Fact]
    public async Task AutenticarAsync_ComSenhaIncorreta_LancaCredenciaisInvalidasException()
    {
        var (_, _, service) = await CriarCenarioComVendedorCadastrado(senha: "senha-secreta");

        await Should.ThrowAsync<CredenciaisInvalidasException>(() =>
            service.AutenticarAsync(new AutenticarVendedorInput("marcos@exemplo.com", "senha-errada")));
    }

    [Fact]
    public async Task AutenticarAsync_ComEmailNaoCadastrado_AindaAssimComputaUmHashParaMitigarTimingAttack()
    {
        var repositorio = new FakeVendedorRepository();
        var hasher = new FakePasswordHasher();
        var service = new AutenticarVendedorService(repositorio, hasher, new FakeGeradorToken());

        await Should.ThrowAsync<CredenciaisInvalidasException>(() =>
            service.AutenticarAsync(new AutenticarVendedorInput("desconhecido@exemplo.com", "qualquer-senha")));

        // antes do patch, um e-mail desconhecido rejeitava sem nunca chamar o hasher — resposta quase
        // instantânea que permitia distinguir "e-mail não existe" de "senha errada" pelo tempo de resposta
        hasher.ChamadasHash.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AutenticarAsync_ComEmailNaoCadastradoOuSenhaIncorreta_LancaExcecaoComMensagemIdentica()
    {
        var (_, _, service) = await CriarCenarioComVendedorCadastrado(senha: "senha-secreta");

        var excecaoEmailDesconhecido = await Should.ThrowAsync<CredenciaisInvalidasException>(() =>
            service.AutenticarAsync(new AutenticarVendedorInput("desconhecido@exemplo.com", "qualquer-senha")));
        var excecaoSenhaErrada = await Should.ThrowAsync<CredenciaisInvalidasException>(() =>
            service.AutenticarAsync(new AutenticarVendedorInput("marcos@exemplo.com", "senha-errada")));

        excecaoEmailDesconhecido.Message.ShouldBe(excecaoSenhaErrada.Message);
    }
}
