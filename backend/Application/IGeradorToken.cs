using Carango.Domain;

namespace Carango.Application;

public record TokenGerado(string Token, DateTime ExpiraEmUtc);

public interface IGeradorToken
{
    TokenGerado Gerar(Vendedor vendedor);
}
