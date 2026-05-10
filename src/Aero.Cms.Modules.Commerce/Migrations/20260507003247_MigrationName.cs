using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aero.Cms.Modules.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class MigrationName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "aero");

            migrationBuilder.RenameTable(
                name: "orders",
                newName: "orders",
                newSchema: "aero");

            migrationBuilder.RenameTable(
                name: "order_items",
                newName: "order_items",
                newSchema: "aero");

            migrationBuilder.RenameTable(
                name: "buyers",
                newName: "buyers",
                newSchema: "aero");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "orders",
                schema: "aero",
                newName: "orders");

            migrationBuilder.RenameTable(
                name: "order_items",
                schema: "aero",
                newName: "order_items");

            migrationBuilder.RenameTable(
                name: "buyers",
                schema: "aero",
                newName: "buyers");
        }
    }
}
