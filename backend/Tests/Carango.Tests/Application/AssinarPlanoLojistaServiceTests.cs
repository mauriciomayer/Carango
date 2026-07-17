using Carango.Application;
using Carango.Domain;
using Carango.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class AssinarPlanoLojistaServiceTests
{
    private static Vendedor CriarLojista() =>
        new("lojista@exemplo.com", "hash", TipoVendedor.Lojista, cnpjRazaoSocial: "12.345.678/0001-90");

    private static Vendedor CriarPessoaFisica() =>
        new("pf@exemplo.com", "hash", TipoVendedor.PessoaFisica);

    private static (FakeVendedorRepository VendedorRepositorio, FakePlanoLojistaRepository PlanoRepositorio, FakeBillingGateway BillingGateway, AssinarPlanoLojistaService Service)
        CriarCenario(Vendedor vendedor, FakeBillingGateway? billingGateway = null)
    {
        var vendedorRepositorio = new FakeVendedorRepository();
        vendedorRepositorio.Vendedores.Add(vendedor);
        var planoRepositorio = new FakePlanoLojistaRepository();
        var gateway = billingGateway ?? new FakeBillingGateway();
        var service = new AssinarPlanoLojistaService(vendedorRepositorio, planoRepositorio, gateway);
        return (vendedorRepositorio, planoRepositorio, gateway, service);
    }

    [Fact]
    public async Task AssinarAsync_ComLojistaSemPlano_CriaPlanoAtivo()
    {
        var lojista = CriarLojista();
        var (_, planoRepositorio, billingGateway, service) = CriarCenario(lojista);

        var plano = await service.AssinarAsync(lojista.Id);

        plano.Status.ShouldBe(StatusPlanoLojista.Ativo);
        plano.VendedorId.ShouldBe(lojista.Id);
        planoRepositorio.Planos.ShouldContain(plano);
        billingGateway.Chamadas.Count.ShouldBe(1);
        billingGateway.Chamadas[0].VendedorId.ShouldBe(lojista.Id);
    }

    [Fact]
    public async Task AssinarAsync_ComPessoaFisica_LancaVendedorNaoELojistaSemCobrar()
    {
        var pessoaFisica = CriarPessoaFisica();
        var (_, planoRepositorio, billingGateway, service) = CriarCenario(pessoaFisica);

        await Should.ThrowAsync<VendedorNaoELojistaException>(() => service.AssinarAsync(pessoaFisica.Id));

        billingGateway.Chamadas.ShouldBeEmpty();
        planoRepositorio.Planos.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssinarAsync_ComLojistaJaComPlanoAtivo_LancaPlanoLojistaJaAtivoSemCobrarDeNovo()
    {
        var lojista = CriarLojista();
        var (_, planoRepositorio, billingGateway, service) = CriarCenario(lojista);
        planoRepositorio.Planos.Add(PlanoLojista.Assinar(lojista.Id));

        await Should.ThrowAsync<PlanoLojistaJaAtivoException>(() => service.AssinarAsync(lojista.Id));

        billingGateway.Chamadas.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssinarAsync_ComCobrancaFalhando_LancaCobrancaFalhouExceptionSemPersistirPlano()
    {
        var lojista = CriarLojista();
        var billingGateway = new FakeBillingGateway(sucesso: false, motivoFalha: "Cartão recusado.");
        var (_, planoRepositorio, _, service) = CriarCenario(lojista, billingGateway);

        var excecao = await Should.ThrowAsync<CobrancaFalhouException>(() => service.AssinarAsync(lojista.Id));

        excecao.Message.ShouldBe("Cartão recusado.");
        planoRepositorio.Planos.ShouldBeEmpty();
    }
}
