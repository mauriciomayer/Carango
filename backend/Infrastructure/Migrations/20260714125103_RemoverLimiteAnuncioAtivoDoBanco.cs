using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carango.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoverLimiteAnuncioAtivoDoBanco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Anuncios_VendedorIdSeAtivo",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "VendedorIdSeAtivo",
                table: "Anuncios");
        }

        // ATENÇÃO (achado no code review): esta Down() só é segura enquanto NENHUM Vendedor tiver 2+
        // Anúncios Ativos simultâneos. Assim que a Story 4.2 estiver em uso real (Lojista com Plano
        // Lojista ativo publicando múltiplos Anúncios), rodar esta Down() falha com violação de
        // índice único (dados existentes já quebram a constraint que ela recria) — na prática,
        // irreversível depois que a feature é usada, não um par Up/Down comum
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VendedorIdSeAtivo",
                table: "Anuncios",
                type: "char(36)",
                nullable: true,
                computedColumnSql: "(CASE WHEN `Status` = 'Ativo' THEN `VendedorId` ELSE NULL END)",
                stored: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Anuncios_VendedorIdSeAtivo",
                table: "Anuncios",
                column: "VendedorIdSeAtivo",
                unique: true);
        }
    }
}
