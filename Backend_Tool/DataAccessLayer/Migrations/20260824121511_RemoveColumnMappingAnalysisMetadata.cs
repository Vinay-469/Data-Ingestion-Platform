using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveColumnMappingAnalysisMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiReason",
                table: "ColumnMappings");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                table: "ColumnMappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiReason",
                table: "ColumnMappings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfidenceScore",
                table: "ColumnMappings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
