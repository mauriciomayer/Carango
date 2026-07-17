using Carango.Application;
using Carango.Domain;

namespace Carango.Tests.TestDoubles;

public class FakeGeradorToken : IGeradorToken
{
    public TokenGerado Gerar(Vendedor vendedor) =>
        new($"fake-token-para-{vendedor.Id}", DateTime.UtcNow.AddMinutes(60));
}
