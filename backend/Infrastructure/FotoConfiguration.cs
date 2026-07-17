using Carango.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carango.Infrastructure;

public class FotoConfiguration : IEntityTypeConfiguration<Foto>
{
    public void Configure(EntityTypeBuilder<Foto> builder)
    {
        builder.ToTable("Fotos");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Url).IsRequired().HasMaxLength(500);
        builder.Property(f => f.Ordem).IsRequired();
    }
}
