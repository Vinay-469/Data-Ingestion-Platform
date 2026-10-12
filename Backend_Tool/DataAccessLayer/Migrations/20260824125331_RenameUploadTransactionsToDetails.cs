using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RenameUploadTransactionsToDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UploadTransactions_Uploads_UploadId",
                table: "UploadTransactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UploadTransactions",
                table: "UploadTransactions");

            migrationBuilder.RenameTable(
                name: "UploadTransactions",
                newName: "UploadTransactionsDetails");

            migrationBuilder.RenameIndex(
                name: "IX_UploadTransactions_UploadId",
                table: "UploadTransactionsDetails",
                newName: "IX_UploadTransactionsDetails_UploadId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UploadTransactionsDetails",
                table: "UploadTransactionsDetails",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_UploadTransactionsDetails_Uploads_UploadId",
                table: "UploadTransactionsDetails",
                column: "UploadId",
                principalTable: "Uploads",
                principalColumn: "UploadId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_PersistUploadBulk
    @ExistingUploadId bigint = 0, @UserId int, @OriginalFileName nvarchar(450), @StoragePath nvarchar(1000),
    @Status nvarchar(50), @UploadedOn datetime2, @Remarks nvarchar(max) = NULL, @IsActive bit, @Finalize bit = 0,
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
            INSERT INTO dbo.Uploads (UserId, OriginalFileName, StoragePath, Status, UploadedOn, Remarks, IsActive)
            VALUES (@UserId, @OriginalFileName, @StoragePath, @Status, @UploadedOn, @Remarks, @IsActive);
            SET @UploadId = CONVERT(bigint, SCOPE_IDENTITY());
            MERGE dbo.Sheets AS target USING @Sheets AS source ON 1 = 0 WHEN NOT MATCHED THEN INSERT (UploadId, SheetName, SheetOrder, TotalRows, TotalColumns) VALUES (@UploadId, source.SheetName, source.SheetOrder, source.TotalRows, source.TotalColumns);
            MERGE dbo.Columns AS target USING @Columns AS source ON 1 = 0 WHEN NOT MATCHED THEN INSERT (SheetId, ColumnOrder, DetectedDataType, AllowNull) VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = (SELECT SheetOrder FROM @Sheets WHERE ClientKey = source.SheetKey)), source.ColumnOrder, source.DetectedDataType, source.AllowNull);
        END;
        IF @Finalize = 1 INSERT INTO dbo.UploadTransactionsDetails (UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage) SELECT @UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage FROM @Transactions;
        COMMIT TRANSACTION; SELECT @UploadId;
    END TRY
    BEGIN CATCH IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END CATCH
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UploadTransactionsDetails_Uploads_UploadId",
                table: "UploadTransactionsDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UploadTransactionsDetails",
                table: "UploadTransactionsDetails");

            migrationBuilder.RenameTable(
                name: "UploadTransactionsDetails",
                newName: "UploadTransactions");

            migrationBuilder.RenameIndex(
                name: "IX_UploadTransactionsDetails_UploadId",
                table: "UploadTransactions",
                newName: "IX_UploadTransactions_UploadId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UploadTransactions",
                table: "UploadTransactions",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_UploadTransactions_Uploads_UploadId",
                table: "UploadTransactions",
                column: "UploadId",
                principalTable: "Uploads",
                principalColumn: "UploadId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
