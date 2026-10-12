using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveColumnLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PersistUploadBulk;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadColumnType;");

            migrationBuilder.DropColumn(
                name: "ColumnName",
                table: "Columns");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Columns");

            migrationBuilder.Sql(@"
CREATE TYPE dbo.UploadColumnType AS TABLE
(
    ClientKey int NOT NULL,
    SheetKey int NOT NULL,
    ColumnOrder int NOT NULL,
    DetectedDataType nvarchar(100) NOT NULL,
    AllowNull bit NOT NULL,
    UniqueValues bigint NOT NULL,
    MaxLength int NULL
);
");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_PersistUploadBulk
    @ExistingUploadId bigint = 0,
    @UserId nvarchar(450), @OriginalFileName nvarchar(450), @StoragePath nvarchar(1000),
    @FileExtension nvarchar(20), @Status nvarchar(50), @UploadedOn datetime2,
    @Remarks nvarchar(max) = NULL, @IsActive bit, @Finalize bit = 0,
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
            INSERT INTO dbo.Uploads (UserId, OriginalFileName, StoragePath, FileExtension, Status, UploadedOn, Remarks, IsActive)
            VALUES (@UserId, @OriginalFileName, @StoragePath, @FileExtension, @Status, @UploadedOn, @Remarks, @IsActive);
            SET @UploadId = CONVERT(bigint, SCOPE_IDENTITY());
            MERGE dbo.Sheets AS target USING @Sheets AS source ON 1 = 0
            WHEN NOT MATCHED THEN INSERT (UploadId, SheetName, SheetOrder, TotalRows, TotalColumns)
                VALUES (@UploadId, source.SheetName, source.SheetOrder, source.TotalRows, source.TotalColumns);
            MERGE dbo.Columns AS target USING @Columns AS source ON 1 = 0
            WHEN NOT MATCHED THEN INSERT (SheetId, ColumnOrder, DetectedDataType, AllowNull, UniqueValues, MaxLength)
                VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = (SELECT SheetOrder FROM @Sheets WHERE ClientKey = source.SheetKey)), source.ColumnOrder, source.DetectedDataType, source.AllowNull, source.UniqueValues, source.MaxLength);
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
        INSERT INTO dbo.CellValues (RowId, ColumnId, Value, DetectedDataType, ValidationStatus)
        SELECT rowMap.RowId, columnInfo.ColumnId, cells.Value, cells.DetectedDataType, cells.ValidationStatus
        FROM @Cells AS cells
        INNER JOIN @RowMap AS rowMap ON rowMap.ClientKey = cells.RowKey
        INNER JOIN @Columns AS sourceColumn ON sourceColumn.ClientKey = cells.ColumnKey
        INNER JOIN @Sheets AS sourceSheet ON sourceSheet.ClientKey = sourceColumn.SheetKey
        INNER JOIN dbo.Sheets AS sheet ON sheet.UploadId = @UploadId AND sheet.SheetOrder = sourceSheet.SheetOrder
        INNER JOIN dbo.Columns AS columnInfo ON columnInfo.SheetId = sheet.SheetId AND columnInfo.ColumnOrder = sourceColumn.ColumnOrder;
        IF @Finalize = 1
            INSERT INTO dbo.UploadTransactions (UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage)
            SELECT @UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage FROM @Transactions;
        COMMIT TRANSACTION;
        SELECT @UploadId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColumnName",
                table: "Columns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Columns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
