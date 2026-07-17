using Microsoft.AspNetCore.Hosting;

namespace Carango.Tests.TestDoubles;

// UseSetting (não ConfigureAppConfiguration) — Program.cs lê builder.Configuration["Jwt:*"] de forma
// síncrona antes de builder.Build(); ConfigureAppConfiguration chega tarde demais nesse cenário de
// minimal hosting. Qualquer teste que ative VendedoresController precisa disso — o controller agora
// depende de AutenticarVendedorService (IGeradorToken), mesmo em requisições que só usam Cadastrar.
internal static class JwtConfiguracaoTeste
{
    public const string Issuer = "Carango.Testes";
    public const string Audience = "Carango.Testes";
    public const string SigningKey = "chave-de-teste-para-jwt-com-pelo-menos-32-caracteres";

    public static void Aplicar(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
    }
}
