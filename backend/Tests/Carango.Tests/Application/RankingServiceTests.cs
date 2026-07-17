using Carango.Application;
using Shouldly;
using Xunit;

namespace Carango.Tests.Application;

public class RankingServiceTests
{
    [Fact]
    public void PriorizaPatrocinado_ComRelevancia_RetornaTrue()
    {
        var service = new RankingService();

        service.PriorizaPatrocinado(OrdenacaoBusca.Relevancia).ShouldBeTrue();
    }

    [Theory]
    [InlineData(OrdenacaoBusca.PrecoAsc)]
    [InlineData(OrdenacaoBusca.PrecoDesc)]
    [InlineData(OrdenacaoBusca.AnoAsc)]
    [InlineData(OrdenacaoBusca.AnoDesc)]
    public void PriorizaPatrocinado_ComOrdenacaoExplicita_RetornaFalse(OrdenacaoBusca ordenacao)
    {
        var service = new RankingService();

        service.PriorizaPatrocinado(ordenacao).ShouldBeFalse();
    }
}
