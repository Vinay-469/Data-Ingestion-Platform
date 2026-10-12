using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnMappingMetadataArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceColumns",
                columns: table => new
                {
                    SourceColumnId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SheetId = table.Column<long>(type: "bigint", nullable: false),
                    SourceColumnName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColumnOrder = table.Column<int>(type: "int", nullable: false),
                    DetectedDataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsMappedToStandard = table.Column<bool>(type: "bit", nullable: false),
                    MappedStandardColumn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSelectedMetadata = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceColumns", x => x.SourceColumnId);
                    table.ForeignKey(
                        name: "FK_SourceColumns_Sheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "Sheets",
                        principalColumn: "SheetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ColumnMappings",
                columns: table => new
                {
                    ColumnMappingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<long>(type: "bigint", nullable: false),
                    SheetId = table.Column<long>(type: "bigint", nullable: false),
                    SourceColumnId = table.Column<long>(type: "bigint", nullable: false),
                    TargetColumnName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MappingMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidenceScore = table.Column<int>(type: "int", nullable: false),
                    AiReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfirmedByCustomer = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IngestionColumnColumnId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnMappings", x => x.ColumnMappingId);
                    table.ForeignKey(
                        name: "FK_ColumnMappings_Columns_IngestionColumnColumnId",
                        column: x => x.IngestionColumnColumnId,
                        principalTable: "Columns",
                        principalColumn: "ColumnId");
                    table.ForeignKey(
                        name: "FK_ColumnMappings_SourceColumns_SourceColumnId",
                        column: x => x.SourceColumnId,
                        principalTable: "SourceColumns",
                        principalColumn: "SourceColumnId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RowMetadataValues",
                columns: table => new
                {
                    RowMetadataValueId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowId = table.Column<long>(type: "bigint", nullable: false),
                    SourceColumnId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedDataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RowMetadataValues", x => x.RowMetadataValueId);
                    table.ForeignKey(
                        name: "FK_RowMetadataValues_Rows_RowId",
                        column: x => x.RowId,
                        principalTable: "Rows",
                        principalColumn: "RowId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RowMetadataValues_SourceColumns_SourceColumnId",
                        column: x => x.SourceColumnId,
                        principalTable: "SourceColumns",
                        principalColumn: "SourceColumnId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMappings_IngestionColumnColumnId",
                table: "ColumnMappings",
                column: "IngestionColumnColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMappings_SourceColumnId",
                table: "ColumnMappings",
                column: "SourceColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMappings_UploadId_SheetId_SourceColumnId",
                table: "ColumnMappings",
                columns: new[] { "UploadId", "SheetId", "SourceColumnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RowMetadataValues_RowId_SourceColumnId",
                table: "RowMetadataValues",
                columns: new[] { "RowId", "SourceColumnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RowMetadataValues_SourceColumnId",
                table: "RowMetadataValues",
                column: "SourceColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceColumns_SheetId_ColumnOrder",
                table: "SourceColumns",
                columns: new[] { "SheetId", "ColumnOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColumnMappings");

            migrationBuilder.DropTable(
                name: "RowMetadataValues");

            migrationBuilder.DropTable(
                name: "SourceColumns");
        }
    }
}
