using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyColumnMappingShadow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ColumnMappings_Columns_IngestionColumnColumnId",
                table: "ColumnMappings");

            migrationBuilder.DropIndex(
                name: "IX_ColumnMappings_IngestionColumnColumnId",
                table: "ColumnMappings");

            migrationBuilder.DropColumn(
                name: "IngestionColumnColumnId",
                table: "ColumnMappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "IngestionColumnColumnId",
                table: "ColumnMappings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMappings_IngestionColumnColumnId",
                table: "ColumnMappings",
                column: "IngestionColumnColumnId");

            migrationBuilder.AddForeignKey(
                name: "FK_ColumnMappings_Columns_IngestionColumnColumnId",
                table: "ColumnMappings",
                column: "IngestionColumnColumnId",
                principalTable: "Columns",
                principalColumn: "ColumnId");
        }
    }
}
