using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using DataAccessLayer.Infrastructure;

#nullable disable

namespace DataAccessLayer.Migrations;

[DbContext(typeof(IngestionDbContext))]
[Migration("20260824130000_ReorderUsersColumns")]
public partial class ReorderUsersColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE [dbo].[Users_Reordered]
(
    [UserId] int IDENTITY(1,1) NOT NULL,
    [UserName] nvarchar(450) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_Users_Reordered_IsActive] DEFAULT ((1)),
    CONSTRAINT [PK_Users_Reordered] PRIMARY KEY CLUSTERED ([UserId])
);

SET IDENTITY_INSERT [dbo].[Users_Reordered] ON;
INSERT INTO [dbo].[Users_Reordered] ([UserId], [UserName], [PasswordHash], [Role], [CreatedOn], [IsActive])
SELECT [UserId], [UserName], [PasswordHash], [Role], [CreatedOn], [IsActive]
FROM [dbo].[Users];
SET IDENTITY_INSERT [dbo].[Users_Reordered] OFF;

DECLARE @maxUserId int = (SELECT ISNULL(MAX([UserId]), 0) FROM [dbo].[Users_Reordered]);
DBCC CHECKIDENT ('dbo.Users_Reordered', RESEED, @maxUserId) WITH NO_INFOMSGS;
CREATE UNIQUE INDEX [IX_Users_Reordered_UserName] ON [dbo].[Users_Reordered] ([UserName]);

DROP TABLE [dbo].[Users];
EXEC sp_rename N'dbo.Users_Reordered', N'Users';
EXEC sp_rename N'dbo.Users.IX_Users_Reordered_UserName', N'IX_Users_UserName', N'INDEX';
EXEC sp_rename N'dbo.Users.PK_Users_Reordered', N'PK_Users';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE [dbo].[Users_Reverted]
(
    [UserName] nvarchar(450) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [CreatedOn] datetime2 NOT NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_Users_Reverted_IsActive] DEFAULT ((1)),
    [UserId] int IDENTITY(1,1) NOT NULL,
    CONSTRAINT [PK_Users_Reverted] PRIMARY KEY CLUSTERED ([UserId])
);

SET IDENTITY_INSERT [dbo].[Users_Reverted] ON;
INSERT INTO [dbo].[Users_Reverted] ([UserName], [PasswordHash], [Role], [CreatedOn], [IsActive], [UserId])
SELECT [UserName], [PasswordHash], [Role], [CreatedOn], [IsActive], [UserId]
FROM [dbo].[Users];
SET IDENTITY_INSERT [dbo].[Users_Reverted] OFF;

DECLARE @maxUserId int = (SELECT ISNULL(MAX([UserId]), 0) FROM [dbo].[Users_Reverted]);
DBCC CHECKIDENT ('dbo.Users_Reverted', RESEED, @maxUserId) WITH NO_INFOMSGS;
CREATE UNIQUE INDEX [IX_Users_Reverted_UserName] ON [dbo].[Users_Reverted] ([UserName]);

DROP TABLE [dbo].[Users];
EXEC sp_rename N'dbo.Users_Reverted', N'Users';
EXEC sp_rename N'dbo.Users.IX_Users_Reverted_UserName', N'IX_Users_UserName', N'INDEX';
EXEC sp_rename N'dbo.Users.PK_Users_Reverted', N'PK_Users';");
    }
}
