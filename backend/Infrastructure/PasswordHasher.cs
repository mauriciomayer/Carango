using Carango.Application;
using Carango.Domain;
using Microsoft.AspNetCore.Identity;

namespace Carango.Infrastructure;

public class PasswordHasher : IPasswordHasher
{
    // PasswordHasher<TUser> é genérico só por extensibilidade; o algoritmo (PBKDF2-HMAC-SHA256)
    // não usa a instância de TUser — passar null! é o padrão aceito para uso standalone
    // (fora do sistema completo de ASP.NET Core Identity). Vendedor não implementa IdentityUser.
    private readonly PasswordHasher<Vendedor> _hasher = new();

    public string Hash(string senha) => _hasher.HashPassword(null!, senha);

    public bool Verificar(string senhaFornecida, string hashArmazenado)
    {
        var resultado = _hasher.VerifyHashedPassword(null!, hashArmazenado, senhaFornecida);
        return resultado != PasswordVerificationResult.Failed;
    }
}
