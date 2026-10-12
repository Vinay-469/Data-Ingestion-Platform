using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullSourceColumnMappingMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MappingMethod",
                table: "SourceColumns",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
            migrationBuilder.Sql("UPDATE dbo.SourceColumns SET MappingMethod = NULL WHERE IsMappedToStandard = 0 OR NULLIF(LTRIM(RTRIM(MappingMethod)), '') IS NULL;");
            migrationBuilder.Sql("UPDATE dbo.SourceColumns SET MappingMethod = 'AI' WHERE MappingMethod = 'Automatic';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE dbo.SourceColumns SET MappingMethod = 'Manual' WHERE MappingMethod IS NULL;");
            migrationBuilder.AlterColumn<string>(
                name: "MappingMethod",
                table: "SourceColumns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Manual",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
