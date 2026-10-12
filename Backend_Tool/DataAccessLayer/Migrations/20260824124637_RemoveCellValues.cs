using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCellValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_PersistUploadBulk;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ProcessUploadStage;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS dbo.UploadCellType;");
            migrationBuilder.DropTable(name: "UploadCellStage", schema: "dbo");
            migrationBuilder.DropTable(
                name: "CellValues");

            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_ProcessUploadStage
    @UploadId bigint, @BatchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        MERGE dbo.Rows AS target
        USING (SELECT SheetOrder, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn, Facility, Manufacture, Product, ProductDescription, CustomerUom, CustomerQty, CustomMetadata FROM dbo.UploadRowStage WHERE UploadId = @UploadId AND BatchId = @BatchId) AS source ON 1 = 0
        WHEN NOT MATCHED THEN INSERT (SheetId, RowNumber, HasDuplicate, HasNull, IsValid, CreatedOn, Facility, Manufacture, Product, ProductDescription, CustomerUom, CustomerQty, CustomMetadata)
            VALUES ((SELECT SheetId FROM dbo.Sheets WHERE UploadId = @UploadId AND SheetOrder = source.SheetOrder), source.RowNumber, source.HasDuplicate, source.HasNull, source.IsValid, source.CreatedOn, source.Facility, source.Manufacture, source.Product, source.ProductDescription, source.CustomerUom, source.CustomerQty, source.CustomMetadata);
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
        IF @Finalize = 1 INSERT INTO dbo.UploadTransactions (UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage) SELECT @UploadId, StartedAt, Status, TotalSheets, TotalRows, TotalColumns, DuplicateRows, NullRows, SkippedRows, ErrorMessage FROM @Transactions;
        COMMIT TRANSACTION; SELECT @UploadId;
    END TRY
    BEGIN CATCH IF XACT_STATE() <> 0 ROLLBACK TRANSACTION; THROW; END CATCH
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CellValues",
                columns: table => new
                {
                    CellId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColumnId = table.Column<long>(type: "bigint", nullable: false),
                    RowId = table.Column<long>(type: "bigint", nullable: false),
                    DetectedDataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CellValues", x => x.CellId);
                    table.ForeignKey(
                        name: "FK_CellValues_Columns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "Columns",
                        principalColumn: "ColumnId");
                    table.ForeignKey(
                        name: "FK_CellValues_Rows_RowId",
                        column: x => x.RowId,
                        principalTable: "Rows",
                        principalColumn: "RowId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CellValues_ColumnId",
                table: "CellValues",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_CellValues_RowId_ColumnId",
                table: "CellValues",
                columns: new[] { "RowId", "ColumnId" },
                unique: true);
        }
    }
}
