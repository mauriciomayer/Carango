using System.Security.Claims;
using System.Text;
using Carango.Application;
using Carango.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Carango.Infrastructure;

public class JwtGeradorToken : IGeradorToken
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _duracao;
    private readonly SigningCredentials _credenciais;

    public JwtGeradorToken(string issuer, string audience, string signingKey, TimeSpan duracao)
    {
        _issuer = issuer;
        _audience = audience;
        _duracao = duracao;

        // HMAC-SHA256 exige no mínimo 256 bits (32 caracteres UTF-8) de chave. Sem essa checagem explícita,
        // uma SigningKey ausente/curta derruba o host com um erro de baixo nível (ex.: IDX10703, "key length
        // is zero", vindo do construtor de SymmetricSecurityKey) em vez de uma mensagem que diga o que
        // configurar — checagem antecipada aqui, com uma mensagem acionável.
        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey ausente ou menor que 32 caracteres. Configure a variável de ambiente " +
                "Jwt__SigningKey com pelo menos 32 caracteres (HMAC-SHA256 exige 256 bits de chave).");
        }

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
    }

    public TokenGerado Gerar(Vendedor vendedor)
    {
        var expiraEmUtc = DateTime.UtcNow.Add(_duracao);

        var descritor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, vendedor.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, vendedor.Email),
                new Claim("tipo", vendedor.Tipo.ToString())
            ]),
            Expires = expiraEmUtc,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = _credenciais
        };

        var token = Handler.CreateToken(descritor);

        return new TokenGerado(token, expiraEmUtc);
    }
}
