namespace Carango.Application;

public interface IPasswordHasher
{
    string Hash(string senha);

    bool Verificar(string senhaFornecida, string hashArmazenado);
}
