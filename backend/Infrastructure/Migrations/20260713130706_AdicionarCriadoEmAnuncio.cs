using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carango.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCriadoEmAnuncio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // valor padrão gerado automaticamente pelo EF Core seria default(DateTime) = 0001-01-01,
            // fora do intervalo válido do DATETIME do MySQL (mínimo 1000-01-01) — corrigido à mão pra
            // um valor válido; qualquer linha existente (não há dados reais neste sandbox) seria
            // carimbada com a data de execução desta migration, na ausência de um histórico real
            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEm",
                table: "Anuncios",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriadoEm",
                table: "Anuncios");
        }
    }
}
