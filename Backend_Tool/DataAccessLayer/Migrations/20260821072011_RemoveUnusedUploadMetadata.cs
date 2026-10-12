using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedUploadMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The bulk procedure and its table-valued parameters must be
            // rebuilt together with the table columns they write.
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
            FROM dbo.UploadRowStage WHERE UploadId = @UploadId AND BatchId = @BatchId
        ) AS source ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT (SheetId, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn)
            VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = source.SheetOrder), source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn)
        OUTPUT source.StageRowId, inserted.RowId INTO @RowMap (StageRowId, RowId);

        INSERT INTO dbo.CellValues (RowId, ColumnId, Value, DetectedDataType, ValidationStatus)
        SELECT map.RowId, columns.ColumnId, cells.Value, cells.DetectedDataType, cells.ValidationStatus
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

            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PersistUploadBulk;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadTransactionType;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadCellType;");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "UploadTransactions");

            migrationBuilder.DropColumn(
                name: "ExecutionTime",
                table: "UploadTransactions");

            migrationBuilder.DropColumn(
                name: "Checksum",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "CompletedOn",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "Uploads");

            migrationBuilder.DropColumn(
                name: "OriginalValue",
                table: "CellValues");

            migrationBuilder.DropColumn(
                name: "OriginalValue",
                table: "UploadCellStage");

            migrationBuilder.Sql(@"
CREATE TYPE dbo.UploadCellType AS TABLE
(
    RowKey int NOT NULL,
    ColumnKey int NOT NULL,
    Value nvarchar(max) NULL,
    DetectedDataType nvarchar(100) NOT NULL,
    ValidationStatus nvarchar(100) NOT NULL
);
CREATE TYPE dbo.UploadTransactionType AS TABLE
(
    StartedAt datetime2 NOT NULL,
    Status nvarchar(50) NOT NULL,
    TotalSheets int NOT NULL,
    TotalRows bigint NOT NULL,
    TotalColumns int NOT NULL,
    DuplicateRows bigint NOT NULL,
    NullRows bigint NOT NULL,
    SkippedRows bigint NOT NULL,
    ErrorMessage nvarchar(max) NULL
);
");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_PersistUploadBulk
    @ExistingUploadId bigint = 0,
    @UserId nvarchar(450), @OriginalFileName nvarchar(450), @StoragePath nvarchar(1000),
    @ContentType nvarchar(200), @FileExtension nvarchar(20), @Status nvarchar(50),
    @UploadedOn datetime2, @Remarks nvarchar(max) = NULL, @IsActive bit, @Finalize bit = 0,
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
            INSERT INTO dbo.Uploads (UserId, OriginalFileName, StoragePath, ContentType, FileExtension, Status, UploadedOn, Remarks, IsActive)
            VALUES (@UserId, @OriginalFileName, @StoragePath, @ContentType, @FileExtension, @Status, @UploadedOn, @Remarks, @IsActive);
            SET @UploadId = CONVERT(bigint, SCOPE_IDENTITY());
            MERGE dbo.Sheets AS target USING @Sheets AS source ON 1 = 0
            WHEN NOT MATCHED THEN INSERT (UploadId, SheetName, SheetOrder, TotalRows, TotalColumns)
                VALUES (@UploadId, source.SheetName, source.SheetOrder, source.TotalRows, source.TotalColumns);
            MERGE dbo.Columns AS target USING @Columns AS source ON 1 = 0
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
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "UploadTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExecutionTime",
                table: "UploadTransactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Checksum",
                table: "Uploads",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedOn",
                table: "Uploads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "Uploads",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "OriginalValue",
                table: "CellValues",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
