using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCustomMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PersistUploadBulk;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ProcessUploadStage;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadRowType;");
            migrationBuilder.DropColumn(name: "CustomMetadata", table: "Rows");
            migrationBuilder.DropColumn(name: "CustomMetadata", table: "UploadRowStage", schema: "dbo");
            migrationBuilder.Sql(@"
CREATE TYPE dbo.UploadRowType AS TABLE
(
    ClientKey int NOT NULL,
    SheetKey int NOT NULL,
    RowNumber bigint NOT NULL,
    HasDuplicate bit NOT NULL,
    HasNull bit NOT NULL,
    IsValid bit NOT NULL,
    CreatedOn datetime2 NOT NULL,
    Facility nvarchar(max) NULL,
    Manufacture nvarchar(max) NULL,
    Product nvarchar(max) NULL,
    ProductDescription nvarchar(max) NULL,
    CustomerUom nvarchar(max) NULL,
    CustomerQty decimal(18,4) NULL
);");
            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_ProcessUploadStage
    @UploadId bigint, @BatchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        MERGE dbo.Rows AS target
        USING (SELECT SheetOrder, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn, Facility, Manufacture, Product, ProductDescription, CustomerUom, CustomerQty FROM dbo.UploadRowStage WHERE UploadId = @UploadId AND BatchId = @BatchId) AS source ON 1 = 0
        WHEN NOT MATCHED THEN INSERT (SheetId, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn, Facility, Manufacture, Product, ProductDescription, CustomerUom, CustomerQty)
            VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = source.SheetOrder), source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn, source.Facility, source.Manufacture, source.Product, source.ProductDescription, source.CustomerUom, source.CustomerQty);
        DELETE FROM dbo.UploadRowStage WHERE UploadId = @UploadId AND BatchId = @BatchId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END CATCH
END;");
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
            migrationBuilder.AddColumn<string>(name: "CustomMetadata", table: "Rows", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CustomMetadata", table: "UploadRowStage", schema: "dbo", type: "nvarchar(max)", nullable: true);
        }
    }
}
