using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Carango.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFkPlanoLojistaVendedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_PlanosLojista_Vendedores_VendedorId",
                table: "PlanosLojista",
                column: "VendedorId",
                principalTable: "Vendedores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanosLojista_Vendedores_VendedorId",
                table: "PlanosLojista");
        }
    }
}
