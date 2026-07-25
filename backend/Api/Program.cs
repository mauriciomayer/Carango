using System.Text;
using System.Text.Json.Serialization;
using Carango.Application;
using Carango.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
    // enum como string no JSON (ex.: "PessoaFisica", não "0") — contrato de domínio em português (AD-8)
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? string.Empty;
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? string.Empty;
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;
var jwtDuracaoMinutos = builder.Configuration.GetValue("Jwt:DuracaoMinutos", 60);
var jwtDuracao = TimeSpan.FromMinutes(jwtDuracaoMinutos);
var mediaStorageBasePath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads", "anuncios");

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default") ?? string.Empty,
    jwtIssuer,
    jwtAudience,
    jwtSigningKey,
    jwtDuracao,
    mediaStorageBasePath);
builder.Services.AddScoped<CadastrarVendedorService>();
builder.Services.AddScoped<AutenticarVendedorService>();
builder.Services.AddScoped<CriarAnuncioService>();
builder.Services.AddScoped<GerenciarAnuncioService>();
builder.Services.AddScoped<BuscarAnunciosService>();
builder.Services.AddScoped<AssinarPlanoLojistaService>();
builder.Services.AddScoped<GerenciarPlanoLojistaService>();
builder.Services.AddScoped<VeiculoReferenciaService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // mesma chave/issuer/audience usados por JwtGeradorToken (Infrastructure) para assinar —
        // se um dia divergirem, tokens válidos passam a ser rejeitados
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
        };
        // sem isso, claims padrão do JWT (ex.: "sub") são remapeadas internamente pra URIs longas de
        // compatibilidade com WS-Federation (ClaimTypes.NameIdentifier) — User.FindFirstValue("sub")
        // retornaria null mesmo com um token válido. Nenhuma story anterior precisou ler claims do
        // token ainda, por isso esse problema só aparece agora (primeiro endpoint [Authorize])
        options.MapInboundClaims = false;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// serve os arquivos salvos por LocalDiskMediaStorage de volta via HTTP (ex.: /uploads/anuncios/{guid}.jpg) —
// nenhuma story anterior serviu wwwroot, primeira vez que isso é necessário
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// classe pública parcial exposta só para WebApplicationFactory<Program> nos testes de integração
public partial class Program;
