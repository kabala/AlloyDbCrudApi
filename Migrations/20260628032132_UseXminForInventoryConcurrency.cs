using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlloyDbCrudApi.Migrations
{
    /// <inheritdoc />
    public partial class UseXminForInventoryConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "inventory_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "inventory_items",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: Array.Empty<byte>());
        }
    }
}
