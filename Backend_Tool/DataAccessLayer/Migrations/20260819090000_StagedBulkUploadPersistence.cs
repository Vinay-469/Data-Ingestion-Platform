using DataAccessLayer.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations;

[DbContext(typeof(IngestionDbContext))]
[Migration("20260819090000_StagedBulkUploadPersistence")]
public partial class StagedBulkUploadPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UploadRowStage",
            schema: "dbo",
            columns: table => new
            {
                StageRowId = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UploadId = table.Column<long>(type: "bigint", nullable: false),
                SheetOrder = table.Column<int>(type: "int", nullable: false),
                RowKey = table.Column<int>(type: "int", nullable: false),
                RowNumber = table.Column<long>(type: "bigint", nullable: false),
                HasDuplicate = table.Column<bool>(type: "bit", nullable: false),
                HasNull = table.Column<bool>(type: "bit", nullable: false),
                IsValid = table.Column<bool>(type: "bit", nullable: false),
                CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_UploadRowStage", x => x.StageRowId));

        migrationBuilder.CreateTable(
            name: "UploadCellStage",
            schema: "dbo",
            columns: table => new
            {
                StageCellId = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UploadId = table.Column<long>(type: "bigint", nullable: false),
                SheetOrder = table.Column<int>(type: "int", nullable: false),
                RowKey = table.Column<int>(type: "int", nullable: false),
                ColumnOrder = table.Column<int>(type: "int", nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OriginalValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DetectedDataType = table.Column<string>(type: "nvarchar(50)", nullable: false),
                ValidationStatus = table.Column<string>(type: "nvarchar(50)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_UploadCellStage", x => x.StageCellId));

        migrationBuilder.CreateIndex("IX_UploadRowStage_BatchId_UploadId", "UploadRowStage", new[] { "BatchId", "UploadId" }, "dbo");
        migrationBuilder.CreateIndex("IX_UploadCellStage_BatchId_UploadId", "UploadCellStage", new[] { "BatchId", "UploadId" }, "dbo");
        migrationBuilder.CreateIndex("IX_UploadRowStage_UploadId", "UploadRowStage", new[] { "UploadId" }, "dbo");
        migrationBuilder.CreateIndex("IX_UploadCellStage_UploadId", "UploadCellStage", new[] { "UploadId" }, "dbo");

        migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_ProcessUploadStage
    @UploadId bigint,
    @BatchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @RowMap TABLE (StageRowId bigint PRIMARY KEY, RowId bigint NOT NULL);
        MERGE dbo.Rows AS target
        USING (
            SELECT StageRowId, SheetOrder, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn
            FROM dbo.UploadRowStage
            WHERE UploadId = @UploadId AND BatchId = @BatchId
        ) AS source ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT (SheetId, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn)
            VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = source.SheetOrder), source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn)
        OUTPUT source.StageRowId, inserted.RowId INTO @RowMap (StageRowId, RowId);

        INSERT INTO dbo.CellValues (RowId, ColumnId, Value, OriginalValue, DetectedDataType, ValidationStatus)
        SELECT map.RowId, columns.ColumnId, cells.Value, cells.OriginalValue, cells.DetectedDataType, cells.ValidationStatus
        FROM dbo.UploadCellStage AS cells
        INNER JOIN dbo.UploadRowStage AS stagedRows ON stagedRows.UploadId = cells.UploadId AND stagedRows.BatchId = cells.BatchId AND stagedRows.SheetOrder = cells.SheetOrder AND stagedRows.RowKey = cells.RowKey
        INNER JOIN @RowMap AS map ON map.StageRowId = stagedRows.StageRowId
        INNER JOIN dbo.Sheets AS sheets ON sheets.UploadId = @UploadId AND sheets.SheetOrder = cells.SheetOrder
        INNER JOIN dbo.Columns AS columns ON columns.SheetId = sheets.SheetId AND columns.ColumnOrder = cells.ColumnOrder
        WHERE cells.UploadId = @UploadId AND cells.BatchId = @BatchId;

        DELETE FROM dbo.UploadCellStage WHERE UploadId = @UploadId AND BatchId = @BatchId;
        DELETE FROM dbo.UploadRowStage WHERE UploadId = @UploadId AND BatchId = @BatchId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ProcessUploadStage;");
        migrationBuilder.DropTable(name: "UploadCellStage", schema: "dbo");
        migrationBuilder.DropTable(name: "UploadRowStage", schema: "dbo");
    }
}
