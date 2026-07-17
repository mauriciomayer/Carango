using Carango.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carango.Infrastructure;

public class PlanoLojistaConfiguration : IEntityTypeConfiguration<PlanoLojista>
{
    public void Configure(EntityTypeBuilder<PlanoLojista> builder)
    {
        builder.ToTable("PlanosLojista");
        builder.HasKey(p => p.Id);

        // FK real (não só um índice) — mesmo padrão já usado em AnuncioConfiguration pra VendedorId;
        // Restrict porque não existe nenhuma feature de exclusão de Vendedor nos épicos ainda (achado
        // no code review: a configuração original não tinha essa FK, inconsistente com o resto do app)
        builder.HasOne<Vendedor>()
            .WithMany()
            .HasForeignKey(p => p.VendedorId)
            .OnDelete(DeleteBehavior.Restrict);

        // um PlanoLojista por Vendedor — registro único cujo Status muda ao longo do tempo
        // (AD-10), não um histórico de assinaturas com uma linha nova por evento
        builder.HasIndex(p => p.VendedorId).IsUnique();

        // string em vez de int — mesmo padrão já usado em Vendedor.Tipo/Anuncio.Status
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
    }
}
