using Carango.Application;
using Carango.Domain;
using Carango.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class GerenciarPlanoLojistaServiceTests
{
    private static readonly Guid VendedorId = Guid.NewGuid();

    [Fact]
    public async Task ObterAsync_SemPlano_RetornaNull()
    {
        var service = new GerenciarPlanoLojistaService(new FakePlanoLojistaRepository());

        var plano = await service.ObterAsync(VendedorId);

        plano.ShouldBeNull();
    }

    [Fact]
    public async Task ObterAsync_ComPlanoExistente_RetornaOPlano()
    {
        var repositorio = new FakePlanoLojistaRepository();
        var planoExistente = PlanoLojista.Assinar(VendedorId);
        repositorio.Planos.Add(planoExistente);
        var service = new GerenciarPlanoLojistaService(repositorio);

        var plano = await service.ObterAsync(VendedorId);

        plano.ShouldBe(planoExistente);
    }

    [Fact]
    public async Task CancelarAsync_ComPlanoAtivo_CancelaComSucesso()
    {
        var repositorio = new FakePlanoLojistaRepository();
        repositorio.Planos.Add(PlanoLojista.Assinar(VendedorId));
        var service = new GerenciarPlanoLojistaService(repositorio);

        var plano = await service.CancelarAsync(VendedorId);

        plano.Status.ShouldBe(StatusPlanoLojista.Cancelado);
    }

    [Fact]
    public async Task CancelarAsync_SemPlanoNenhum_LancaPlanoLojistaNaoEncontrado()
    {
        var service = new GerenciarPlanoLojistaService(new FakePlanoLojistaRepository());

        await Should.ThrowAsync<PlanoLojistaNaoEncontradoException>(() => service.CancelarAsync(VendedorId));
    }

    [Fact]
    public async Task CancelarAsync_ComPlanoJaCancelado_LancaInvalidOperationException()
    {
        var repositorio = new FakePlanoLojistaRepository();
        var plano = PlanoLojista.Assinar(VendedorId);
        plano.Cancelar();
        repositorio.Planos.Add(plano);
        var service = new GerenciarPlanoLojistaService(repositorio);

        await Should.ThrowAsync<InvalidOperationException>(() => service.CancelarAsync(VendedorId));
    }
}
