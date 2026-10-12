using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceUploadUserNameWithUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Uploads",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE upload
SET upload.UserId = appUser.UserId
FROM dbo.Uploads AS upload
INNER JOIN dbo.Users AS appUser ON appUser.UserName = upload.UserName;
IF EXISTS (SELECT 1 FROM dbo.Uploads WHERE UserId IS NULL)
    THROW 51000, 'Cannot convert uploads: one or more records have no matching user.', 1;");

            migrationBuilder.AlterColumn<int>(name: "UserId", table: "Uploads", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.DropIndex(name: "IX_Uploads_UserName_UploadedOn", table: "Uploads");
            migrationBuilder.DropColumn(name: "UserName", table: "Uploads");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UserId_UploadedOn",
                table: "Uploads",
                columns: new[] { "UserId", "UploadedOn" });

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Users_UserId",
                table: "Uploads",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_PersistUploadBulk
    @ExistingUploadId bigint = 0, @UserId int, @OriginalFileName nvarchar(450), @StoragePath nvarchar(1000),
    @FileExtension nvarchar(20), @Status nvarchar(50), @UploadedOn datetime2, @Remarks nvarchar(max) = NULL, @IsActive bit, @Finalize bit = 0,
    @Sheets dbo.UploadSheetType READONLY, @Columns dbo.UploadColumnType READONLY, @Rows dbo.UploadRowType READONLY,
    @Cells dbo.UploadCellType READONLY, @Transactions dbo.UploadTransactionType READONLY
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @UploadId bigint = @ExistingUploadId;
        IF @UploadId = 0
        BEGIN
            INSERT INTO dbo.Uploads (UserId, OriginalFileName, StoragePath, FileExtension, Status, UploadedOn, Remarks, IsActive)
            VALUES (@UserId, @OriginalFileName, @StoragePath, @FileExtension, @Status, @UploadedOn, @Remarks, @IsActive);
            SET @UploadId = CONVERT(bigint, SCOPE_IDENTITY());
            MERGE dbo.Sheets AS target USING @Sheets AS source ON 1 = 0 WHEN NOT MATCHED THEN INSERT (UploadId, SheetName, SheetOrder, TotalRows, TotalColumns) VALUES (@UploadId, source.SheetName, source.SheetOrder, source.TotalRows, source.TotalColumns);
            MERGE dbo.Columns AS target USING @Columns AS source ON 1 = 0 WHEN NOT MATCHED THEN INSERT (SheetId, ColumnOrder, DetectedDataType, AllowNull, UniqueValues) VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = (SELECT SheetOrder FROM @Sheets WHERE ClientKey = source.SheetKey)), source.ColumnOrder, source.DetectedDataType, source.AllowNull, source.UniqueValues);
        END;
        DECLARE @RowMap TABLE (ClientKey int PRIMARY KEY, RowId bigint NOT NULL);
        MERGE dbo.Rows AS target
        USING (SELECT source.ClientKey, sheet.SheetId, source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn, source.Facility, source.Manufacture, source.Product, source.ProductDescription, source.CustomerUom, source.CustomerQty, source.CustomMetadata FROM @Rows AS source INNER JOIN @Sheets AS sourceSheet ON sourceSheet.ClientKey = source.SheetKey INNER JOIN dbo.Sheets AS sheet ON sheet.UploadId = @UploadId AND sheet.SheetOrder = sourceSheet.SheetOrder) AS source ON 1 = 0
        WHEN NOT MATCHED THEN INSERT (SheetId, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn, Facility, Manufacture, Product, ProductDescription, CustomerUom, CustomerQty, CustomMetadata) VALUES (source.SheetId, source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn, source.Facility, source.Manufacture, source.Product, source.ProductDescription, source.CustomerUom, source.CustomerQty, source.CustomMetadata)
        OUTPUT source.ClientKey, inserted.RowId INTO @RowMap (ClientKey, RowId);
        INSERT INTO dbo.CellValues (RowId, ColumnId, Value, DetectedDataType, ValidationStatus) SELECT rowMap.RowId, columnInfo.ColumnId, cells.Value, cells.DetectedDataType, cells.ValidationStatus FROM @Cells AS cells INNER JOIN @RowMap AS rowMap ON rowMap.ClientKey = cells.RowKey INNER JOIN @Columns AS sourceColumn ON sourceColumn.ClientKey = cells.ColumnKey INNER JOIN @Sheets AS sourceSheet ON sourceSheet.ClientKey = sourceColumn.SheetKey INNER JOIN dbo.Sheets AS sheet ON sheet.UploadId = @UploadId AND sheet.SheetOrder = sourceSheet.SheetOrder INNER JOIN dbo.Columns AS columnInfo ON columnInfo.SheetId = sheet.SheetId AND columnInfo.ColumnOrder = sourceColumn.ColumnOrder;
        IF @Finalize = 1 INSERT INTO dbo.UploadTransactions (UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage) SELECT @UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage FROM @Transactions;
        COMMIT TRANSACTION; SELECT @UploadId;
    END TRY
    BEGIN CATCH IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END CATCH
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Uploads",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE upload SET upload.UserName = appUser.UserName FROM dbo.Uploads AS upload INNER JOIN dbo.Users AS appUser ON appUser.UserId = upload.UserId;");
            migrationBuilder.DropForeignKey(name: "FK_Uploads_Users_UserId", table: "Uploads");
            migrationBuilder.DropIndex(name: "IX_Uploads_UserId_UploadedOn", table: "Uploads");
            migrationBuilder.DropColumn(name: "UserId", table: "Uploads");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UserName_UploadedOn",
                table: "Uploads",
                columns: new[] { "UserName", "UploadedOn" });
        }
    }
}
