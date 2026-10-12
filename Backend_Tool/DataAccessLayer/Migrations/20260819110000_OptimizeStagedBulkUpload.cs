using DataAccessLayer.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations;

[DbContext(typeof(IngestionDbContext))]
[Migration("20260819110000_OptimizeStagedBulkUpload")]
public partial class OptimizeStagedBulkUpload : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_UploadRowStage_BatchId_UploadId", "UploadRowStage", "dbo");
        migrationBuilder.DropIndex("IX_UploadCellStage_BatchId_UploadId", "UploadCellStage", "dbo");

        migrationBuilder.CreateIndex(
            name: "IX_UploadRowStage_BulkLookup",
            table: "UploadRowStage",
            schema: "dbo",
            columns: new[] { "BatchId", "UploadId", "SheetOrder", "RowKey" },
            unique: false);

        migrationBuilder.CreateIndex(
            name: "IX_UploadCellStage_BulkLookup",
            table: "UploadCellStage",
            schema: "dbo",
            columns: new[] { "BatchId", "UploadId", "SheetOrder", "RowKey", "ColumnOrder" },
            unique: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_UploadRowStage_BulkLookup", "UploadRowStage", "dbo");
        migrationBuilder.DropIndex("IX_UploadCellStage_BulkLookup", "UploadCellStage", "dbo");

        migrationBuilder.CreateIndex("IX_UploadRowStage_BatchId_UploadId", "UploadRowStage", new[] { "BatchId", "UploadId" }, "dbo");
        migrationBuilder.CreateIndex("IX_UploadCellStage_BatchId_UploadId", "UploadCellStage", new[] { "BatchId", "UploadId" }, "dbo");
    }
}
