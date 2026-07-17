using Carango.Application;

namespace Carango.Tests.TestDoubles;

public class FakePasswordHasher : IPasswordHasher
{
    private const string Prefixo = "fake-hash:";

    public int ChamadasHash { get; private set; }

    public string Hash(string senha)
    {
        ChamadasHash++;
        return Prefixo + senha;
    }

    public bool Verificar(string senhaFornecida, string hashArmazenado) => Hash(senhaFornecida) == hashArmazenado;
}
