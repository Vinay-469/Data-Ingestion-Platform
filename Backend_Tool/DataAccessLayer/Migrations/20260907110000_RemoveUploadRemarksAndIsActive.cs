using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations;

public partial class RemoveUploadRemarksAndIsActive : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PersistUploadBulk;");
        migrationBuilder.DropColumn(name: "Remarks", table: "Uploads");
        migrationBuilder.DropColumn(name: "IsActive", table: "Uploads");
        migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_PersistUploadBulk
    @ExistingUploadId bigint = 0, @UserId int, @OriginalFileName nvarchar(450),
    @Status nvarchar(50), @UploadedOn datetime2, @Finalize bit = 0,
    @Sheets dbo.UploadSheetType READONLY, @Columns dbo.UploadColumnType READONLY, @Rows dbo.UploadRowType READONLY,
    @Transactions dbo.UploadTransactionType READONLY
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @UploadId bigint = @ExistingUploadId;
        IF @UploadId = 0
        BEGIN
            INSERT INTO dbo.Uploads (UserId, OriginalFileName, Status, UploadedOn)
            VALUES (@UserId, @OriginalFileName, @Status, @UploadedOn);
            SET @UploadId = CONVERT(bigint, SCOPE_IDENTITY());
            MERGE dbo.Sheets AS target USING @Sheets AS source ON 1 = 0
            WHEN NOT MATCHED THEN INSERT (UploadId, SheetName, SheetOrder, TotalRows, TotalColumns)
            VALUES (@UploadId, source.SheetName, source.SheetOrder, source.TotalRows, source.TotalColumns);
            MERGE dbo.Columns AS target USING @Columns AS source ON 1 = 0
            WHEN NOT MATCHED THEN INSERT (SheetId, ColumnOrder, DetectedDataType, AllowNull)
            VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = (SELECT SheetOrder FROM @Sheets WHERE ClientKey = source.SheetKey)), source.ColumnOrder, source.DetectedDataType, source.AllowNull);
        END;
        IF @Finalize = 1
            INSERT INTO dbo.UploadTransactionsDetails (UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage)
            SELECT @UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage FROM @Transactions;
        COMMIT TRANSACTION; SELECT @UploadId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Remarks", table: "Uploads", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsActive", table: "Uploads", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PersistUploadBulk;");
    }
}
