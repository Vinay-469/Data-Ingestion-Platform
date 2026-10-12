using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardCustomerDataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PersistUploadBulk;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ProcessUploadStage;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadRowType;");

            migrationBuilder.AddColumn<string>(
                name: "CustomMetadata",
                table: "Rows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerQty",
                table: "Rows",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerUom",
                table: "Rows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Facility",
                table: "Rows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacture",
                table: "Rows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Product",
                table: "Rows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductDescription",
                table: "Rows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(name: "Facility", table: "UploadRowStage", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Manufacture", table: "UploadRowStage", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Product", table: "UploadRowStage", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ProductDescription", table: "UploadRowStage", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CustomerUom", table: "UploadRowStage", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "CustomerQty", table: "UploadRowStage", type: "decimal(18,4)", precision: 18, scale: 4, nullable: true);
            migrationBuilder.AddColumn<string>(name: "CustomMetadata", table: "UploadRowStage", type: "nvarchar(max)", nullable: true);

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
    CustomerQty decimal(18,4) NULL,
    CustomMetadata nvarchar(max) NULL
);
");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_ProcessUploadStage
    @UploadId bigint, @BatchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @RowMap TABLE (StageRowId bigint PRIMARY KEY, RowId bigint NOT NULL);
        MERGE dbo.Rows AS target
        USING (SELECT StageRowId, SheetOrder, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn, Facility, Manufacture, Product, ProductDescription, CustomerUom, CustomerQty, CustomMetadata FROM dbo.UploadRowStage WHERE UploadId = @UploadId AND BatchId = @BatchId) AS source ON 1 = 0
        WHEN NOT MATCHED THEN INSERT (SheetId, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn, Facility, Manufacture, Product, ProductDescription, CustomerUom, CustomerQty, CustomMetadata)
            VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = source.SheetOrder), source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn, source.Facility, source.Manufacture, source.Product, source.ProductDescription, source.CustomerUom, source.CustomerQty, source.CustomMetadata)
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
    BEGIN CATCH IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END CATCH
END;
");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_PersistUploadBulk
    @ExistingUploadId bigint = 0, @UserName nvarchar(450), @OriginalFileName nvarchar(450), @StoragePath nvarchar(1000),
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
            INSERT INTO dbo.Uploads (UserName, OriginalFileName, StoragePath, FileExtension, Status, UploadedOn, Remarks, IsActive) VALUES (@UserName, @OriginalFileName, @StoragePath, @FileExtension, @Status, @UploadedOn, @Remarks, @IsActive);
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
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomMetadata",
                table: "Rows");

            migrationBuilder.DropColumn(
                name: "CustomerQty",
                table: "Rows");

            migrationBuilder.DropColumn(
                name: "CustomerUom",
                table: "Rows");

            migrationBuilder.DropColumn(
                name: "Facility",
                table: "Rows");

            migrationBuilder.DropColumn(
                name: "Manufacture",
                table: "Rows");

            migrationBuilder.DropColumn(
                name: "Product",
                table: "Rows");

            migrationBuilder.DropColumn(
                name: "ProductDescription",
                table: "Rows");
        }
    }
}
