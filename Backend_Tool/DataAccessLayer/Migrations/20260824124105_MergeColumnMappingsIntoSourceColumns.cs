using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class MergeColumnMappingsIntoSourceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ConfirmedByCustomer",
                table: "SourceColumns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "SourceColumns",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "MappingMethod",
                table: "SourceColumns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE sourceColumn
SET sourceColumn.MappedStandardColumn = mapping.TargetColumnName,
    sourceColumn.IsMappedToStandard = CASE WHEN mapping.TargetColumnName IS NULL THEN 0 ELSE 1 END,
    sourceColumn.MappingMethod = mapping.MappingMethod,
    sourceColumn.ConfirmedByCustomer = mapping.ConfirmedByCustomer,
    sourceColumn.CreatedOn = mapping.CreatedOn
FROM dbo.SourceColumns AS sourceColumn
INNER JOIN dbo.ColumnMappings AS mapping ON mapping.SourceColumnId = sourceColumn.SourceColumnId;");

            migrationBuilder.DropTable(
                name: "ColumnMappings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedByCustomer",
                table: "SourceColumns");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "SourceColumns");

            migrationBuilder.DropColumn(
                name: "MappingMethod",
                table: "SourceColumns");

            migrationBuilder.CreateTable(
                name: "ColumnMappings",
                columns: table => new
                {
                    ColumnMappingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceColumnId = table.Column<long>(type: "bigint", nullable: false),
                    ConfirmedByCustomer = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MappingMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SheetId = table.Column<long>(type: "bigint", nullable: false),
                    TargetColumnName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UploadId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnMappings", x => x.ColumnMappingId);
                    table.ForeignKey(
                        name: "FK_ColumnMappings_SourceColumns_SourceColumnId",
                        column: x => x.SourceColumnId,
                        principalTable: "SourceColumns",
                        principalColumn: "SourceColumnId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMappings_SourceColumnId",
                table: "ColumnMappings",
                column: "SourceColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMappings_UploadId_SheetId_SourceColumnId",
                table: "ColumnMappings",
                columns: new[] { "UploadId", "SheetId", "SourceColumnId" },
                unique: true);
        }
    }
}
