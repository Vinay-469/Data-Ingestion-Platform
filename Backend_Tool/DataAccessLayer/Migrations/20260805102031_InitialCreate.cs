using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DuplicateAudits",
                columns: table => new
                {
                    AuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<long>(type: "bigint", nullable: false),
                    SheetId = table.Column<long>(type: "bigint", nullable: false),
                    RowId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionTaken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateAudits", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "Uploads",
                columns: table => new
                {
                    UploadId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uploads", x => x.UploadId);
                });

            migrationBuilder.CreateTable(
                name: "ValidationAudits",
                columns: table => new
                {
                    ValidationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<long>(type: "bigint", nullable: false),
                    SheetId = table.Column<long>(type: "bigint", nullable: false),
                    RowId = table.Column<long>(type: "bigint", nullable: false),
                    ColumnId = table.Column<long>(type: "bigint", nullable: true),
                    ValidationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidationMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationAudits", x => x.ValidationId);
                });

            migrationBuilder.CreateTable(
                name: "Sheets",
                columns: table => new
                {
                    SheetId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<long>(type: "bigint", nullable: false),
                    SheetName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SheetOrder = table.Column<int>(type: "int", nullable: false),
                    TotalRows = table.Column<long>(type: "bigint", nullable: false),
                    TotalColumns = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sheets", x => x.SheetId);
                    table.ForeignKey(
                        name: "FK_Sheets_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "UploadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UploadTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionTime = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalSheets = table.Column<int>(type: "int", nullable: false),
                    TotalRows = table.Column<long>(type: "bigint", nullable: false),
                    TotalColumns = table.Column<int>(type: "int", nullable: false),
                    DuplicateRows = table.Column<long>(type: "bigint", nullable: false),
                    NullRows = table.Column<long>(type: "bigint", nullable: false),
                    SkippedRows = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadTransactions", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_UploadTransactions_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "UploadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Columns",
                columns: table => new
                {
                    ColumnId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SheetId = table.Column<long>(type: "bigint", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColumnOrder = table.Column<int>(type: "int", nullable: false),
                    DetectedDataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowNull = table.Column<bool>(type: "bit", nullable: false),
                    UniqueValues = table.Column<long>(type: "bigint", nullable: false),
                    MaxLength = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Columns", x => x.ColumnId);
                    table.ForeignKey(
                        name: "FK_Columns_Sheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "Sheets",
                        principalColumn: "SheetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rows",
                columns: table => new
                {
                    RowId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SheetId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<long>(type: "bigint", nullable: false),
                    HasDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    HasNull = table.Column<bool>(type: "bit", nullable: false),
                    IsValid = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rows", x => x.RowId);
                    table.ForeignKey(
                        name: "FK_Rows_Sheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "Sheets",
                        principalColumn: "SheetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CellValues",
                columns: table => new
                {
                    CellId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowId = table.Column<long>(type: "bigint", nullable: false),
                    ColumnId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedDataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CellValues", x => x.CellId);
                    table.ForeignKey(
                        name: "FK_CellValues_Columns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "Columns",
                        principalColumn: "ColumnId",
                        onDelete: ReferentialAction.NoAction);
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

            migrationBuilder.CreateIndex(
                name: "IX_Columns_SheetId_ColumnOrder",
                table: "Columns",
                columns: new[] { "SheetId", "ColumnOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Rows_SheetId_RowNumber",
                table: "Rows",
                columns: new[] { "SheetId", "RowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Sheets_UploadId_SheetOrder",
                table: "Sheets",
                columns: new[] { "UploadId", "SheetOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UserId_UploadedOn",
                table: "Uploads",
                columns: new[] { "UserId", "UploadedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadTransactions_UploadId",
                table: "UploadTransactions",
                column: "UploadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CellValues");

            migrationBuilder.DropTable(
                name: "DuplicateAudits");

            migrationBuilder.DropTable(
                name: "UploadTransactions");

            migrationBuilder.DropTable(
                name: "ValidationAudits");

            migrationBuilder.DropTable(
                name: "Columns");

            migrationBuilder.DropTable(
                name: "Rows");

            migrationBuilder.DropTable(
                name: "Sheets");

            migrationBuilder.DropTable(
                name: "Uploads");
        }
    }
}

