using DataAccessLayer.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations;

[DbContext(typeof(IngestionDbContext))]
[Migration("20260821070000_RemoveStoredFileName")]
public partial class RemoveStoredFileName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_PersistUploadBulk
    @ExistingUploadId bigint = 0,
    @UserId nvarchar(450), @OriginalFileName nvarchar(450), @StoragePath nvarchar(1000),
    @ContentType nvarchar(200), @FileExtension nvarchar(20), @FileSize bigint, @Checksum nvarchar(128), @Status nvarchar(50),
    @UploadedOn datetime2, @CompletedOn datetime2 = NULL, @Remarks nvarchar(max) = NULL, @IsActive bit, @Finalize bit = 0,
    @Sheets dbo.UploadSheetType READONLY, @Columns dbo.UploadColumnType READONLY, @Rows dbo.UploadRowType READONLY,
    @Cells dbo.UploadCellType READONLY, @Transactions dbo.UploadTransactionType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @UploadId bigint = @ExistingUploadId;
        IF @UploadId = 0
        BEGIN
            INSERT INTO dbo.Uploads (UserId, OriginalFileName, StoragePath, ContentType, FileExtension, FileSize, Checksum, Status, UploadedOn, CompletedOn, Remarks, IsActive)
            VALUES (@UserId, @OriginalFileName, @StoragePath, @ContentType, @FileExtension, @FileSize, @Checksum, @Status, @UploadedOn, @CompletedOn, @Remarks, @IsActive);
            SET @UploadId = CONVERT(bigint, SCOPE_IDENTITY());
            MERGE dbo.Sheets AS target
            USING @Sheets AS source ON 1 = 0
            WHEN NOT MATCHED THEN INSERT (UploadId, SheetName, SheetOrder, TotalRows, TotalColumns)
                VALUES (@UploadId, source.SheetName, source.SheetOrder, source.TotalRows, source.TotalColumns);
            MERGE dbo.Columns AS target
            USING @Columns AS source ON 1 = 0
            WHEN NOT MATCHED THEN INSERT (SheetId, ColumnName, DisplayName, ColumnOrder, DetectedDataType, AllowNull, UniqueValues, MaxLength)
                VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = (SELECT SheetOrder FROM @Sheets WHERE ClientKey = source.SheetKey)), source.ColumnName, source.DisplayName, source.ColumnOrder, source.DetectedDataType, source.AllowNull, source.UniqueValues, source.MaxLength);
        END;

        DECLARE @RowMap TABLE (ClientKey int PRIMARY KEY, RowId bigint NOT NULL);
        MERGE dbo.Rows AS target
        USING (
            SELECT source.ClientKey, sheet.SheetId, source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn
            FROM @Rows AS source
            INNER JOIN @Sheets AS sourceSheet ON sourceSheet.ClientKey = source.SheetKey
            INNER JOIN dbo.Sheets AS sheet ON sheet.UploadId = @UploadId AND sheet.SheetOrder = sourceSheet.SheetOrder
        ) AS source ON 1 = 0
        WHEN NOT MATCHED THEN INSERT (SheetId, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn)
            VALUES (source.SheetId, source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn)
        OUTPUT source.ClientKey, inserted.RowId INTO @RowMap (ClientKey, RowId);

        INSERT INTO dbo.CellValues (RowId, ColumnId, Value, OriginalValue, DetectedDataType, ValidationStatus)
        SELECT rowMap.RowId, columnInfo.ColumnId, cells.Value, cells.OriginalValue, cells.DetectedDataType, cells.ValidationStatus
        FROM @Cells AS cells
        INNER JOIN @RowMap AS rowMap ON rowMap.ClientKey = cells.RowKey
        INNER JOIN @Columns AS sourceColumn ON sourceColumn.ClientKey = cells.ColumnKey
        INNER JOIN @Sheets AS sourceSheet ON sourceSheet.ClientKey = sourceColumn.SheetKey
        INNER JOIN dbo.Sheets AS sheet ON sheet.UploadId = @UploadId AND sheet.SheetOrder = sourceSheet.SheetOrder
        INNER JOIN dbo.Columns AS columnInfo ON columnInfo.SheetId = sheet.SheetId AND columnInfo.ColumnOrder = sourceColumn.ColumnOrder;

        IF @Finalize = 1
            INSERT INTO dbo.UploadTransactions (UploadId, StartedAt, CompletedAt, ExecutionTime, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage)
            SELECT @UploadId, StartedAt, CompletedAt, ExecutionTime, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage FROM @Transactions;
        COMMIT TRANSACTION;
        SELECT @UploadId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
");

        migrationBuilder.DropColumn(
            name: "StoredFileName",
            table: "Uploads");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "StoredFileName",
            table: "Uploads",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");
    }
}
