using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carango.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPatrocinadoAnuncio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Patrocinado",
                table: "Anuncios",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Patrocinado",
                table: "Anuncios");
        }
    }
}
