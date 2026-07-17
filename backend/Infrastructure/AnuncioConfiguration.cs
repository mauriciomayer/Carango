using Carango.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carango.Infrastructure;

public class AnuncioConfiguration : IEntityTypeConfiguration<Anuncio>
{
    public void Configure(EntityTypeBuilder<Anuncio> builder)
    {
        builder.ToTable("Anuncios");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.VendedorId).IsRequired();

        // FK real (não só um índice) — Restrict porque não existe nenhuma feature de exclusão de
        // Vendedor nos épicos ainda; impedir a exclusão em vez de deixar Anuncios órfãos é o padrão
        // mais seguro na ausência de uma decisão explícita
        builder.HasOne<Vendedor>()
            .WithMany()
            .HasForeignKey(a => a.VendedorId)
            .OnDelete(DeleteBehavior.Restrict);

        // REMOVIDO na Story 4.2: até então, uma coluna gerada (VendedorIdSeAtivo) impunha "no máximo
        // 1 Anúncio Ativo por Vendedor" no próprio banco, incondicionalmente. A partir desta story, um
        // Lojista com Plano Lojista ativo PODE ter vários Anúncios ativos — MySQL não permite que uma
        // coluna gerada referencie outra tabela (PlanosLojista), então não dá pra condicionar esse
        // índice ao status do plano. A garantia de "no máximo 1, exceto Lojista com plano ativo" passa
        // a existir só na checagem de Application (CriarAnuncioService/GerenciarAnuncioService) — trade-off
        // documentado nos Dev Notes da Story 4.2, não uma omissão: a proteção contra corrida entre duas
        // publicações/reativações concorrentes do mesmo Vendedor Pessoa Física fica mais fraca (só
        // Application, sem rede de segurança do banco).

        builder.Property(a => a.Marca).HasMaxLength(100);
        builder.Property(a => a.Modelo).HasMaxLength(100);
        builder.Property(a => a.Versao).HasMaxLength(100);
        builder.Property(a => a.Descricao).HasMaxLength(4000);
        builder.Property(a => a.Estado).HasMaxLength(2);
        builder.Property(a => a.Cidade).HasMaxLength(100);
        builder.Property(a => a.Preco).HasColumnType("decimal(10,2)");

        // primeiro campo de data/hora do projeto — usado só pra ordenar a listagem (Story 2.5),
        // sempre UTC (datetime(6) preserva a precisão de tick do .NET, evita truncar milissegundos)
        builder.Property(a => a.CriadoEm).HasColumnType("datetime(6)").IsRequired();

        // string em vez de int — espelha ARCHITECTURE-SPINE.md § Structural Seed (Anuncio.Status: "ativo | pausado | vendido"),
        // mesmo padrão já usado pra Vendedor.Tipo desde a Story 1.2. "Rascunho" é uma extensão desta story (ver Dev Notes).
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        // Fotos não tem setter público (só o getter que expõe o campo privado _fotos) — o EF Core usa o
        // campo de apoio automaticamente por convenção de nome (_fotos), sem precisar configurar isso à mão
        builder.HasMany(a => a.Fotos)
            .WithOne()
            .HasForeignKey("AnuncioId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
