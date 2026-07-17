using Carango.Infrastructure;
using Shouldly;
using Xunit;

namespace Carango.Tests.Infrastructure;

public class JwtGeradorTokenTests
{
    [Theory]
    [InlineData("")]
    [InlineData("chave-curta")]
    public void Construtor_ComSigningKeyMenorQue32Caracteres_LancaInvalidOperationExceptionComMensagemAcionavel(string signingKey)
    {
        var excecao = Should.Throw<InvalidOperationException>(() =>
            new JwtGeradorToken("Carango", "Carango", signingKey, TimeSpan.FromMinutes(60)));

        excecao.Message.ShouldContain("Jwt:SigningKey");
    }

    [Fact]
    public void Construtor_ComSigningKeyDe32CaracteresOuMais_NaoLancaExcecao()
    {
        Should.NotThrow(() =>
            new JwtGeradorToken("Carango", "Carango", "chave-de-teste-com-pelo-menos-32-caracteres", TimeSpan.FromMinutes(60)));
    }
}
