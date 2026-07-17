using Carango.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carango.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string jwtIssuer,
        string jwtAudience,
        string jwtSigningKey,
        TimeSpan jwtDuracao,
        string mediaStorageBasePath)
    {
        // ServerVersion explícito (não AutoDetect) — AutoDetect abriria uma conexão real com o MySQL
        // só para configurar o DbContext, o que quebraria o host em ambientes sem MySQL disponível
        // (ex.: WebApplicationFactory nos testes de integração, que substituem os repositórios por fakes
        // e nunca deveriam precisar de um MySQL real de pé).
        services.AddDbContext<CarangoDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));

        services.AddScoped<IVendedorRepository, VendedorRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IGeradorToken>(
            new JwtGeradorToken(jwtIssuer, jwtAudience, jwtSigningKey, jwtDuracao));

        services.AddScoped<IAnuncioRepository, AnuncioRepository>();
        services.AddSingleton<IMediaStorage>(new LocalDiskMediaStorage(mediaStorageBasePath));

        // RankingService (AD-5) é lógica pura, sem estado — Singleton seguro. MockBillingGateway
        // (AD-4) é o placeholder da Story 4.1 enquanto a Pergunta Aberta 3 do PRD (gateway real)
        // não é respondida pelo cliente — sempre "aprova" em produção (construtor default);
        // substituir por uma implementação real de IBillingGateway aqui quando o gateway for definido
        services.AddSingleton<IRankingService, RankingService>();
        services.AddSingleton<IBillingGateway, MockBillingGateway>();

        services.AddScoped<IPlanoLojistaRepository, PlanoLojistaRepository>();

        // primeira integração externa do projeto (AD-12, Story 2.6) — API pública da Fipe, sem
        // autenticação obrigatória. Timeout curto (5s): AC #3 exige falhar rápido e mostrar erro
        // claro, não pendurar a requisição do Vendedor
        services.AddMemoryCache();
        services.AddHttpClient<IVeiculoReferenciaGateway, FipeVeiculoReferenciaGateway>(client =>
        {
            // barra final obrigatória — sem ela, um caminho relativo com barra inicial (ex.
            // "/carros/marcas") substitui todo o path do BaseAddress em vez de anexar (regra de
            // combinação de Uri do .NET), resultando em https://parallelum.com.br/carros/marcas
            // (404) em vez de https://parallelum.com.br/fipe/api/v1/carros/marcas
            client.BaseAddress = new Uri("https://parallelum.com.br/fipe/api/v1/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
