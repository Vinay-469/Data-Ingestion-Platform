using DataAccessLayer.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations;

[DbContext(typeof(IngestionDbContext))]
[Migration("20260824140000_ReorderUploadsColumns")]
public partial class ReorderUploadsColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Sheets] DROP CONSTRAINT [FK_Sheets_Uploads_UploadId];
ALTER TABLE [dbo].[UploadTransactions] DROP CONSTRAINT [FK_UploadTransactions_Uploads_UploadId];
ALTER TABLE [dbo].[Uploads] DROP CONSTRAINT [FK_Uploads_Users_UserId];
DROP INDEX [IX_Uploads_UserId_UploadedOn] ON [dbo].[Uploads];

CREATE TABLE [dbo].[Uploads_Reordered]
(
    [UploadId] bigint IDENTITY(1,1) NOT NULL,
    [UserId] int NOT NULL,
    [OriginalFileName] nvarchar(max) NOT NULL,
    [StoragePath] nvarchar(max) NOT NULL,
    [FileExtension] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [UploadedOn] datetime2 NOT NULL,
    [Remarks] nvarchar(max) NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_Uploads_Reordered_IsActive] DEFAULT ((1)),
    CONSTRAINT [PK_Uploads_Reordered] PRIMARY KEY CLUSTERED ([UploadId])
);

SET IDENTITY_INSERT [dbo].[Uploads_Reordered] ON;
INSERT INTO [dbo].[Uploads_Reordered] ([UploadId], [UserId], [OriginalFileName], [StoragePath], [FileExtension], [Status], [UploadedOn], [Remarks], [IsActive])
SELECT [UploadId], [UserId], [OriginalFileName], [StoragePath], [FileExtension], [Status], [UploadedOn], [Remarks], [IsActive]
FROM [dbo].[Uploads];
SET IDENTITY_INSERT [dbo].[Uploads_Reordered] OFF;

DECLARE @maxUploadId bigint = (SELECT ISNULL(MAX([UploadId]), 0) FROM [dbo].[Uploads_Reordered]);
DBCC CHECKIDENT ('dbo.Uploads_Reordered', RESEED, @maxUploadId) WITH NO_INFOMSGS;
CREATE INDEX [IX_Uploads_Reordered_UserId_UploadedOn] ON [dbo].[Uploads_Reordered] ([UserId], [UploadedOn]);

DROP TABLE [dbo].[Uploads];
EXEC sp_rename N'dbo.Uploads_Reordered', N'Uploads';
EXEC sp_rename N'dbo.Uploads.PK_Uploads_Reordered', N'PK_Uploads';
EXEC sp_rename N'dbo.Uploads.IX_Uploads_Reordered_UserId_UploadedOn', N'IX_Uploads_UserId_UploadedOn', N'INDEX';

ALTER TABLE [dbo].[Uploads] ADD CONSTRAINT [FK_Uploads_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]);
ALTER TABLE [dbo].[Sheets] ADD CONSTRAINT [FK_Sheets_Uploads_UploadId] FOREIGN KEY ([UploadId]) REFERENCES [dbo].[Uploads] ([UploadId]);
ALTER TABLE [dbo].[UploadTransactions] ADD CONSTRAINT [FK_UploadTransactions_Uploads_UploadId] FOREIGN KEY ([UploadId]) REFERENCES [dbo].[Uploads] ([UploadId]);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Sheets] DROP CONSTRAINT [FK_Sheets_Uploads_UploadId];
ALTER TABLE [dbo].[UploadTransactions] DROP CONSTRAINT [FK_UploadTransactions_Uploads_UploadId];
ALTER TABLE [dbo].[Uploads] DROP CONSTRAINT [FK_Uploads_Users_UserId];
DROP INDEX [IX_Uploads_UserId_UploadedOn] ON [dbo].[Uploads];

CREATE TABLE [dbo].[Uploads_Reverted]
(
    [UploadId] bigint IDENTITY(1,1) NOT NULL,
    [OriginalFileName] nvarchar(max) NOT NULL,
    [StoragePath] nvarchar(max) NOT NULL,
    [FileExtension] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [UploadedOn] datetime2 NOT NULL,
    [Remarks] nvarchar(max) NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_Uploads_Reverted_IsActive] DEFAULT ((1)),
    [UserId] int NOT NULL,
    CONSTRAINT [PK_Uploads_Reverted] PRIMARY KEY CLUSTERED ([UploadId])
);

SET IDENTITY_INSERT [dbo].[Uploads_Reverted] ON;
INSERT INTO [dbo].[Uploads_Reverted] ([UploadId], [OriginalFileName], [StoragePath], [FileExtension], [Status], [UploadedOn], [Remarks], [IsActive], [UserId])
SELECT [UploadId], [OriginalFileName], [StoragePath], [FileExtension], [Status], [UploadedOn], [Remarks], [IsActive], [UserId]
FROM [dbo].[Uploads];
SET IDENTITY_INSERT [dbo].[Uploads_Reverted] OFF;

DECLARE @maxUploadId bigint = (SELECT ISNULL(MAX([UploadId]), 0) FROM [dbo].[Uploads_Reverted]);
DBCC CHECKIDENT ('dbo.Uploads_Reverted', RESEED, @maxUploadId) WITH NO_INFOMSGS;
CREATE INDEX [IX_Uploads_Reverted_UserId_UploadedOn] ON [dbo].[Uploads_Reverted] ([UserId], [UploadedOn]);
DROP TABLE [dbo].[Uploads];
EXEC sp_rename N'dbo.Uploads_Reverted', N'Uploads';
EXEC sp_rename N'dbo.Uploads.PK_Uploads_Reverted', N'PK_Uploads';
EXEC sp_rename N'dbo.Uploads.IX_Uploads_Reverted_UserId_UploadedOn', N'IX_Uploads_UserId_UploadedOn', N'INDEX';
ALTER TABLE [dbo].[Uploads] ADD CONSTRAINT [FK_Uploads_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]);
ALTER TABLE [dbo].[Sheets] ADD CONSTRAINT [FK_Sheets_Uploads_UploadId] FOREIGN KEY ([UploadId]) REFERENCES [dbo].[Uploads] ([UploadId]);
ALTER TABLE [dbo].[UploadTransactions] ADD CONSTRAINT [FK_UploadTransactions_Uploads_UploadId] FOREIGN KEY ([UploadId]) REFERENCES [dbo].[Uploads] ([UploadId]);");
    }
}
