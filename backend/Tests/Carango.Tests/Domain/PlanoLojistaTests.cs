using Carango.Domain;
using Shouldly;
using Xunit;

namespace Carango.Tests.Domain;

public class PlanoLojistaTests
{
    private static readonly Guid VendedorId = Guid.NewGuid();

    [Fact]
    public void Assinar_ComVendedorIdValido_RetornaPlanoAtivo()
    {
        var plano = PlanoLojista.Assinar(VendedorId);

        plano.Id.ShouldNotBe(Guid.Empty);
        plano.VendedorId.ShouldBe(VendedorId);
        plano.Status.ShouldBe(StatusPlanoLojista.Ativo);
    }

    [Fact]
    public void Assinar_ComVendedorIdVazio_LancaArgumentException()
    {
        Should.Throw<ArgumentException>(() => PlanoLojista.Assinar(Guid.Empty));
    }

    [Fact]
    public void Cancelar_DeAtivo_MarcaComoCancelado()
    {
        var plano = PlanoLojista.Assinar(VendedorId);

        plano.Cancelar();

        plano.Status.ShouldBe(StatusPlanoLojista.Cancelado);
    }

    [Fact]
    public void Cancelar_DeJaCancelado_LancaInvalidOperationException()
    {
        var plano = PlanoLojista.Assinar(VendedorId);
        plano.Cancelar();

        Should.Throw<InvalidOperationException>(() => plano.Cancelar());
    }
}
