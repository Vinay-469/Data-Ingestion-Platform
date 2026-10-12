using DataAccessLayer.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations;

[DbContext(typeof(IngestionDbContext))]
[Migration("20260812090000_BulkUploadProcedure")]
public partial class BulkUploadProcedure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TYPE dbo.UploadSheetType AS TABLE
(
    ClientKey int NOT NULL,
    SheetName nvarchar(450) NOT NULL,
    SheetOrder int NOT NULL,
    TotalRows bigint NOT NULL,
    TotalColumns int NOT NULL
);
CREATE TYPE dbo.UploadColumnType AS TABLE
(
    ClientKey int NOT NULL,
    SheetKey int NOT NULL,
    ColumnName nvarchar(450) NOT NULL,
    DisplayName nvarchar(450) NOT NULL,
    ColumnOrder int NOT NULL,
    DetectedDataType nvarchar(100) NOT NULL,
    AllowNull bit NOT NULL,
    UniqueValues bigint NOT NULL,
    MaxLength int NULL
);
CREATE TYPE dbo.UploadRowType AS TABLE
(
    ClientKey int NOT NULL,
    SheetKey int NOT NULL,
    RowNumber bigint NOT NULL,
    HasDuplicate bit NOT NULL,
    HasNull bit NOT NULL,
    IsValid bit NOT NULL,
    CreatedOn datetime2 NOT NULL
);
CREATE TYPE dbo.UploadCellType AS TABLE
(
    RowKey int NOT NULL,
    ColumnKey int NOT NULL,
    Value nvarchar(max) NULL,
    OriginalValue nvarchar(max) NULL,
    DetectedDataType nvarchar(100) NOT NULL,
    ValidationStatus nvarchar(100) NOT NULL
);
CREATE TYPE dbo.UploadTransactionType AS TABLE
(
    StartedAt datetime2 NOT NULL,
    CompletedAt datetime2 NULL,
    ExecutionTime bigint NOT NULL,
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
    @UserId nvarchar(450), @OriginalFileName nvarchar(450), @StoredFileName nvarchar(450), @StoragePath nvarchar(1000),
    @ContentType nvarchar(200), @FileExtension nvarchar(20), @FileSize bigint, @Checksum nvarchar(128), @Status nvarchar(50),
    @UploadedOn datetime2, @CompletedOn datetime2 = NULL, @Remarks nvarchar(max) = NULL, @IsActive bit,
    @Sheets dbo.UploadSheetType READONLY, @Columns dbo.UploadColumnType READONLY, @Rows dbo.UploadRowType READONLY,
    @Cells dbo.UploadCellType READONLY, @Transactions dbo.UploadTransactionType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        INSERT INTO dbo.Uploads (UserId, OriginalFileName, StoredFileName, StoragePath, ContentType, FileExtension, FileSize, Checksum, Status, UploadedOn, CompletedOn, Remarks, IsActive)
        VALUES (@UserId, @OriginalFileName, @StoredFileName, @StoragePath, @ContentType, @FileExtension, @FileSize, @Checksum, @Status, @UploadedOn, @CompletedOn, @Remarks, @IsActive);
        DECLARE @UploadId bigint = CONVERT(bigint, SCOPE_IDENTITY());
        DECLARE @SheetMap TABLE (ClientKey int PRIMARY KEY, SheetId bigint NOT NULL);
        DECLARE @ColumnMap TABLE (ClientKey int PRIMARY KEY, ColumnId bigint NOT NULL);
        DECLARE @RowMap TABLE (ClientKey int PRIMARY KEY, RowId bigint NOT NULL);

        MERGE dbo.Sheets AS target
        USING @Sheets AS source ON 1 = 0
        WHEN NOT MATCHED THEN INSERT (UploadId, SheetName, SheetOrder, TotalRows, TotalColumns)
            VALUES (@UploadId, source.SheetName, source.SheetOrder, source.TotalRows, source.TotalColumns)
        OUTPUT source.ClientKey, inserted.SheetId INTO @SheetMap (ClientKey, SheetId);

        MERGE dbo.Columns AS target
        USING @Columns AS source ON 1 = 0
        WHEN NOT MATCHED THEN INSERT (SheetId, ColumnName, DisplayName, ColumnOrder, DetectedDataType, AllowNull, UniqueValues, MaxLength)
            VALUES ((SELECT SheetId FROM @SheetMap WHERE ClientKey = source.SheetKey), source.ColumnName, source.DisplayName, source.ColumnOrder, source.DetectedDataType, source.AllowNull, source.UniqueValues, source.MaxLength)
        OUTPUT source.ClientKey, inserted.ColumnId INTO @ColumnMap (ClientKey, ColumnId);

        MERGE dbo.Rows AS target
        USING @Rows AS source ON 1 = 0
        WHEN NOT MATCHED THEN INSERT (SheetId, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn)
            VALUES ((SELECT SheetId FROM @SheetMap WHERE ClientKey = source.SheetKey), source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn)
        OUTPUT source.ClientKey, inserted.RowId INTO @RowMap (ClientKey, RowId);

        INSERT INTO dbo.CellValues (RowId, ColumnId, Value, OriginalValue, DetectedDataType, ValidationStatus)
        SELECT rowMap.RowId, columnMap.ColumnId, cells.Value, cells.OriginalValue, cells.DetectedDataType, cells.ValidationStatus
        FROM @Cells AS cells
        INNER JOIN @RowMap AS rowMap ON rowMap.ClientKey = cells.RowKey
        INNER JOIN @ColumnMap AS columnMap ON columnMap.ClientKey = cells.ColumnKey;

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
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PersistUploadBulk;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadTransactionType;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadCellType;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadRowType;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadColumnType;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadSheetType;");
    }
}
