using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class ConvertUserIdToIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE [Users] ADD [NewUserId] int IDENTITY(1,1) NOT NULL;");
            migrationBuilder.DropPrimaryKey(name: "PK_Users", table: "Users");
            migrationBuilder.DropColumn(name: "UserId", table: "Users");
            migrationBuilder.RenameColumn(name: "NewUserId", table: "Users", newName: "UserId");
            migrationBuilder.AddPrimaryKey(name: "PK_Users", table: "Users", column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "OldUserId", table: "Users", type: "nvarchar(450)", nullable: false, defaultValue: "");
            migrationBuilder.Sql("UPDATE [Users] SET [OldUserId] = CONVERT(nvarchar(450), [UserId]);");
            migrationBuilder.DropPrimaryKey(name: "PK_Users", table: "Users");
            migrationBuilder.DropColumn(name: "UserId", table: "Users");
            migrationBuilder.RenameColumn(name: "OldUserId", table: "Users", newName: "UserId");
            migrationBuilder.AddPrimaryKey(name: "PK_Users", table: "Users", column: "UserId");
        }
    }
}
