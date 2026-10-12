SELECT * FROM dbo.Users;

SELECT * FROM dbo.Uploads;

SELECT * FROM dbo.UploadTransactionsDetails;

SELECT * FROM dbo.Sheets;

SELECT * FROM dbo.Columns;

SELECT * FROM dbo.SourceColumns;

SELECT * FROM dbo.Rows;

SELECT * FROM dbo.RowMetadataValues;

SELECT * FROM dbo.ApplicationLogs;


Select * from dbo.ValidationAudits;

SELECT *
FROM dbo.ApplicationLogs
WHERE Level = 'Error'