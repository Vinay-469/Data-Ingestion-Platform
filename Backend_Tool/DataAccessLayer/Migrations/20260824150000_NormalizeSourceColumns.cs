using DataAccessLayer.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations;

[DbContext(typeof(IngestionDbContext))]
[Migration("20260824150000_NormalizeSourceColumns")]
public partial class NormalizeSourceColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "ColumnId", table: "SourceColumns", type: "bigint", nullable: true);
        migrationBuilder.Sql(@"
UPDATE sourceColumn
SET sourceColumn.ColumnId = dataColumn.ColumnId
FROM dbo.SourceColumns AS sourceColumn
INNER JOIN dbo.Columns AS dataColumn
    ON dataColumn.SheetId = sourceColumn.SheetId
   AND dataColumn.ColumnOrder = sourceColumn.ColumnOrder;
IF EXISTS (SELECT 1 FROM dbo.SourceColumns WHERE ColumnId IS NULL)
    THROW 51001, 'Cannot normalize SourceColumns: one or more source columns have no matching Columns record.', 1;");
        migrationBuilder.DropForeignKey(name: "FK_SourceColumns_Sheets_SheetId", table: "SourceColumns");
        migrationBuilder.DropIndex(name: "IX_SourceColumns_SheetId_ColumnOrder", table: "SourceColumns");
        migrationBuilder.AlterColumn<long>(name: "ColumnId", table: "SourceColumns", type: "bigint", nullable: false, oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.DropColumn(name: "SheetId", table: "SourceColumns");
        migrationBuilder.DropColumn(name: "ColumnOrder", table: "SourceColumns");
        migrationBuilder.DropColumn(name: "DetectedDataType", table: "SourceColumns");
        migrationBuilder.CreateIndex(name: "IX_SourceColumns_ColumnId", table: "SourceColumns", column: "ColumnId", unique: true);
        migrationBuilder.AddForeignKey(name: "FK_SourceColumns_Columns_ColumnId", table: "SourceColumns", column: "ColumnId", principalTable: "Columns", principalColumn: "ColumnId", onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "SheetId", table: "SourceColumns", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<int>(name: "ColumnOrder", table: "SourceColumns", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(name: "DetectedDataType", table: "SourceColumns", type: "nvarchar(max)", nullable: true);
        migrationBuilder.Sql(@"
UPDATE sourceColumn
SET sourceColumn.SheetId = dataColumn.SheetId,
    sourceColumn.ColumnOrder = dataColumn.ColumnOrder,
    sourceColumn.DetectedDataType = dataColumn.DetectedDataType
FROM dbo.SourceColumns AS sourceColumn
INNER JOIN dbo.Columns AS dataColumn ON dataColumn.ColumnId = sourceColumn.ColumnId;");
        migrationBuilder.DropForeignKey(name: "FK_SourceColumns_Columns_ColumnId", table: "SourceColumns");
        migrationBuilder.DropIndex(name: "IX_SourceColumns_ColumnId", table: "SourceColumns");
        migrationBuilder.DropColumn(name: "ColumnId", table: "SourceColumns");
        migrationBuilder.AlterColumn<long>(name: "SheetId", table: "SourceColumns", type: "bigint", nullable: false, oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.AlterColumn<int>(name: "ColumnOrder", table: "SourceColumns", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "DetectedDataType", table: "SourceColumns", type: "nvarchar(max)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_SourceColumns_SheetId_ColumnOrder", table: "SourceColumns", columns: new[] { "SheetId", "ColumnOrder" }, unique: true);
        migrationBuilder.AddForeignKey(name: "FK_SourceColumns_Sheets_SheetId", table: "SourceColumns", column: "SheetId", principalTable: "Sheets", principalColumn: "SheetId", onDelete: ReferentialAction.Cascade);
    }
}
