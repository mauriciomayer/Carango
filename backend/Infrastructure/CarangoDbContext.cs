using Carango.Domain;
using Microsoft.EntityFrameworkCore;

namespace Carango.Infrastructure;

public class CarangoDbContext : DbContext
{
    public CarangoDbContext(DbContextOptions<CarangoDbContext> options) : base(options)
    {
    }

    public DbSet<Vendedor> Vendedores => Set<Vendedor>();
    public DbSet<Anuncio> Anuncios => Set<Anuncio>();
    public DbSet<PlanoLojista> PlanosLojista => Set<PlanoLojista>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CarangoDbContext).Assembly);
    }
}
