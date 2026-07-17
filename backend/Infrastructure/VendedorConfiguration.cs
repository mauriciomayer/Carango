using Carango.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carango.Infrastructure;

public class VendedorConfiguration : IEntityTypeConfiguration<Vendedor>
{
    public void Configure(EntityTypeBuilder<Vendedor> builder)
    {
        builder.ToTable("Vendedores");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(v => v.Email).IsUnique();

        builder.Property(v => v.Telefone).HasMaxLength(30);
        builder.Property(v => v.SenhaHash).IsRequired();

        // string em vez de int — espelha ARCHITECTURE-SPINE.md § Structural Seed (Vendedor.Tipo: "PessoaFisica | Lojista")
        builder.Property(v => v.Tipo).HasConversion<string>().HasMaxLength(20);

        builder.Property(v => v.CnpjRazaoSocial).HasMaxLength(200);
    }
}
