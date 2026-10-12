using ApplicationLayer.Interfaces;
using CoreModels.Entities;
using CoreModels.DTOs;
using DataAccessLayer.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace DataAccessLayer.Infrastructure;

public sealed class UploadRepository(IngestionDbContext database, IConfiguration configuration) : IUploadRepository
{
    private DateTime Now => DateTime.UtcNow.AddHours(configuration.GetValue<double?>("Application:Time:IndiaStandardTimeOffsetHours") ?? 5.5);
    public Task AddAsync(Upload upload, Token token) => database.Uploads.AddAsync(upload, token).AsTask();
    public Task SaveChangesAsync(Token token) => database.SaveChangesAsync(token);
    public Task<int?> GetUserIdAsync(string userName, Token token) => database.Users.Where(user => user.UserName == userName && user.IsActive).Select(user => (int?)user.UserId).SingleOrDefaultAsync(token);
    public async Task DeleteUploadAsync(long uploadId, Token token)
    {
        await database.Database.ExecuteSqlInterpolatedAsync($"DELETE r FROM dbo.Rows r INNER JOIN dbo.Sheets s ON s.SheetId = r.SheetId WHERE s.UploadId = {uploadId}", token);
        await database.Database.ExecuteSqlInterpolatedAsync($"DELETE c FROM dbo.Columns c INNER JOIN dbo.Sheets s ON s.SheetId = c.SheetId WHERE s.UploadId = {uploadId}", token);
        await database.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.ValidationAudits WHERE UploadId = {uploadId}", token);
        await database.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.DuplicateAudits WHERE UploadId = {uploadId}", token);
        await database.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.UploadTransactionsDetails WHERE UploadId = {uploadId}", token);
        await database.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.Sheets WHERE UploadId = {uploadId}", token);
        await database.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.Uploads WHERE UploadId = {uploadId}", token);
    }
    public async Task<long> BulkPersistAsync(Upload upload, Token token)
    {
        // Keep the in-memory staging table bounded while reducing database
        // round-trips for large workbooks. This gives SQL Server enough rows
        // per bulk copy without retaining the complete workbook in EF.
        // Use fewer, larger staged batches to reduce stored-procedure and
        // transaction round-trips while keeping memory bounded.
        const int batchSize = 250_000;
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(token);

        long? uploadId = upload.UploadId > 0 ? upload.UploadId : null;
        if (uploadId is null)
            uploadId = await ExecuteBulkBatchAsync(connection, upload, [], null, false, token);

        for (var sheetIndex = 0; sheetIndex < upload.Sheets.Count; sheetIndex++)
        {
            var sheet = upload.Sheets.ElementAt(sheetIndex);
            while (sheet.Rows.Count > 0)
            {
                var batch = sheet.Rows.Take(batchSize).Select(row => (Sheet: sheet, SheetIndex: sheetIndex, Row: row)).ToList();
                var batchId = Guid.NewGuid();
                await BulkCopyStageAsync((SqlConnection)connection, uploadId.Value, batchId, batch, upload, token);
                await ProcessStageAsync(connection, uploadId.Value, batchId, token);
                if (sheet.Rows is List<IngestionRow> rowList) rowList.RemoveRange(0, batch.Count);
                else foreach (var item in batch) sheet.Rows.Remove(item.Row);
            }
        }

        // Metadata is created on the first call. Only the final call needs to
        // send the transaction/finalization payload; intermediate confirmation
        // batches should not execute another metadata procedure call.
        if (upload.Transactions.Count > 0)
            await ExecuteBulkBatchAsync(connection, upload, [], uploadId, true, token);

        upload.UploadId = uploadId.Value;
        return uploadId.Value;
    }

    private static async Task BulkCopyStageAsync(SqlConnection connection, long uploadId, Guid batchId, IReadOnlyList<(IngestionSheet Sheet, int SheetIndex, IngestionRow Row)> rows, Upload upload, Token token)
    {
        var stagedRows = new DataTable();
        stagedRows.Columns.Add("BatchId", typeof(Guid)); stagedRows.Columns.Add("UploadId", typeof(long)); stagedRows.Columns.Add("SheetOrder", typeof(int)); stagedRows.Columns.Add("RowKey", typeof(int)); stagedRows.Columns.Add("RowNumber", typeof(long)); stagedRows.Columns.Add("HasDuplicate", typeof(bool)); stagedRows.Columns.Add("HasNull", typeof(bool)); stagedRows.Columns.Add("IsValid", typeof(bool)); stagedRows.Columns.Add("CreatedOn", typeof(DateTime)); stagedRows.Columns.Add("Facility", typeof(string)); stagedRows.Columns.Add("Manufacture", typeof(string)); stagedRows.Columns.Add("Product", typeof(string)); stagedRows.Columns.Add("ProductDescription", typeof(string)); stagedRows.Columns.Add("CustomerUom", typeof(string)); stagedRows.Columns.Add("CustomerQty", typeof(decimal));
        var rowKey = 1;
        foreach (var item in rows)
        {
            stagedRows.Rows.Add(batchId, uploadId, item.Sheet.SheetOrder, rowKey, item.Row.RowNumber, item.Row.HasDuplicate, item.Row.HasNull, item.Row.IsValid, item.Row.CreatedOn, (object?)item.Row.Facility ?? DBNull.Value, (object?)item.Row.Manufacture ?? DBNull.Value, (object?)item.Row.Product ?? DBNull.Value, (object?)item.Row.ProductDescription ?? DBNull.Value, (object?)item.Row.CustomerUom ?? DBNull.Value, (object?)item.Row.CustomerQty ?? DBNull.Value);
            rowKey++;
        }

        using (var rowsCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepNulls, null) { DestinationTableName = "dbo.UploadRowStage", BatchSize = Math.Max(stagedRows.Rows.Count, 1), BulkCopyTimeout = 900, EnableStreaming = true })
        {
            foreach (DataColumn column in stagedRows.Columns) rowsCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            await rowsCopy.WriteToServerAsync(stagedRows, token);
        }
    }

    private static async Task ProcessStageAsync(System.Data.Common.DbConnection connection, long uploadId, Guid batchId, Token token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.usp_ProcessUploadStage";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 900;
        command.Parameters.Add(new SqlParameter("@UploadId", SqlDbType.BigInt) { Value = uploadId });
        command.Parameters.Add(new SqlParameter("@BatchId", SqlDbType.UniqueIdentifier) { Value = batchId });
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<long> ExecuteBulkBatchAsync(
        System.Data.Common.DbConnection connection,
        Upload upload,
        IReadOnlyList<(IngestionSheet Sheet, int SheetIndex, IngestionRow Row)> batch,
        long? existingUploadId,
        bool finalize,
        Token token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.usp_PersistUploadBulk";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 300;
        command.Parameters.Add(new SqlParameter("@ExistingUploadId", SqlDbType.BigInt) { Value = existingUploadId ?? 0L });
        command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = upload.UserId });
        command.Parameters.Add(new SqlParameter("@OriginalFileName", SqlDbType.NVarChar, 450) { Value = upload.OriginalFileName });
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = upload.Status });
        // UploadService already creates UploadedOn using the configured IST offset.
        // Do not add 330 minutes again here; doing so shifts the timestamp twice.
        command.Parameters.Add(new SqlParameter("@UploadedOn", SqlDbType.DateTime2) { Value = upload.UploadedOn });
        command.Parameters.Add(new SqlParameter("@Finalize", SqlDbType.Bit) { Value = finalize });
        command.Parameters.Add(CreateSheetsParameter(upload));
        command.Parameters.Add(CreateColumnsParameter(upload));
        command.Parameters.Add(CreateRowsParameter(batch));
        command.Parameters.Add(CreateTransactionParameter(finalize ? upload : null));
        var result = await command.ExecuteScalarAsync(token);
        return Convert.ToInt64(result);
    }

    private static SqlParameter CreateSheetsParameter(Upload upload)
    {
        var table = new DataTable(); table.Columns.Add("ClientKey", typeof(int)); table.Columns.Add("SheetName", typeof(string)); table.Columns.Add("SheetOrder", typeof(int)); table.Columns.Add("TotalRows", typeof(long)); table.Columns.Add("TotalColumns", typeof(int));
        for (var i = 0; i < upload.Sheets.Count; i++) { var sheet = upload.Sheets.ElementAt(i); table.Rows.Add(i + 1, sheet.SheetName, sheet.SheetOrder, sheet.TotalRows, sheet.TotalColumns); }
        return Structured("@Sheets", "dbo.UploadSheetType", table);
    }

    private static SqlParameter CreateColumnsParameter(Upload upload)
    {
        var table = new DataTable(); table.Columns.Add("ClientKey", typeof(int)); table.Columns.Add("SheetKey", typeof(int)); table.Columns.Add("ColumnOrder", typeof(int)); table.Columns.Add("DetectedDataType", typeof(string)); table.Columns.Add("AllowNull", typeof(bool));
        var key = 1; for (var s = 0; s < upload.Sheets.Count; s++) foreach (var column in upload.Sheets.ElementAt(s).Columns) table.Rows.Add(key++, s + 1, column.ColumnOrder, column.DetectedDataType, column.AllowNull);
        return Structured("@Columns", "dbo.UploadColumnType", table);
    }

    private static SqlParameter CreateRowsParameter(IReadOnlyList<(IngestionSheet Sheet, int SheetIndex, IngestionRow Row)> rows)
    {
        var table = new DataTable(); table.Columns.Add("ClientKey", typeof(int)); table.Columns.Add("SheetKey", typeof(int)); table.Columns.Add("RowNumber", typeof(long)); table.Columns.Add("HasDuplicate", typeof(bool)); table.Columns.Add("HasNull", typeof(bool)); table.Columns.Add("IsValid", typeof(bool)); table.Columns.Add("CreatedOn", typeof(DateTime)); table.Columns.Add("Facility", typeof(string)); table.Columns.Add("Manufacture", typeof(string)); table.Columns.Add("Product", typeof(string)); table.Columns.Add("ProductDescription", typeof(string)); table.Columns.Add("CustomerUom", typeof(string)); table.Columns.Add("CustomerQty", typeof(decimal));
        var key = 1; foreach (var item in rows) table.Rows.Add(key++, item.SheetIndex + 1, item.Row.RowNumber, item.Row.HasDuplicate, item.Row.HasNull, item.Row.IsValid, item.Row.CreatedOn, (object?)item.Row.Facility ?? DBNull.Value, (object?)item.Row.Manufacture ?? DBNull.Value, (object?)item.Row.Product ?? DBNull.Value, (object?)item.Row.ProductDescription ?? DBNull.Value, (object?)item.Row.CustomerUom ?? DBNull.Value, (object?)item.Row.CustomerQty ?? DBNull.Value);
        return Structured("@Rows", "dbo.UploadRowType", table);
    }

    private static SqlParameter CreateTransactionParameter(Upload? upload)
    {
        var table = new DataTable(); table.Columns.Add("StartedAt", typeof(DateTime)); table.Columns.Add("Status", typeof(string)); table.Columns.Add("TotalSheets", typeof(int)); table.Columns.Add("TotalRows", typeof(long)); table.Columns.Add("TotalColumns", typeof(int)); table.Columns.Add("DuplicateRows", typeof(long)); table.Columns.Add("NullRows", typeof(long)); table.Columns.Add("SkippedRows", typeof(long)); table.Columns.Add("ErrorMessage", typeof(string));
        if (upload is not null) { var transaction = upload.Transactions.LastOrDefault() ?? new UploadTransaction(); table.Rows.Add(transaction.StartedAt, transaction.Status, transaction.TotalSheets, transaction.TotalRows, transaction.TotalColumns, transaction.DuplicateRows, transaction.NullRows, transaction.SkippedRows, (object?)transaction.ErrorMessage ?? DBNull.Value); }
        return Structured("@Transactions", "dbo.UploadTransactionType", table);
    }

    private static SqlParameter Structured(string name, string typeName, DataTable value) => new(name, SqlDbType.Structured) { TypeName = typeName, Value = value };
    public async Task AddAuditsAsync(IEnumerable<DuplicateAudit> duplicateAudits, IEnumerable<ValidationAudit> validationAudits, Token token)
    {
        await database.DuplicateAudits.AddRangeAsync(duplicateAudits, token);
        await database.ValidationAudits.AddRangeAsync(validationAudits, token);
    }
    public async Task PersistColumnMetadataAsync(long uploadId, ParsedWorkbook workbook, UploadConfirmationRequest request, Token token)
    {
        var sheets = await database.Sheets.Include(sheet => sheet.Columns).Where(sheet => sheet.UploadId == uploadId).OrderBy(sheet => sheet.SheetOrder).ToListAsync(token);
        var sources = new List<SourceColumn>();
        foreach (var sheet in sheets)
        {
            var parsed = workbook.Sheets.FirstOrDefault(item => item.Order == sheet.SheetOrder);
            if (parsed is null) continue;
            foreach (var column in parsed.Columns)
            {
                var mapping = request.Mappings?.FirstOrDefault(item => item.SheetOrder == sheet.SheetOrder && item.ColumnOrder == column.Order);
                var selected = mapping is null && request.SelectedMetadataColumns?.Any(item => item.SheetOrder == sheet.SheetOrder && item.ColumnOrder == column.Order) == true;
                var targetColumn = sheet.Columns.FirstOrDefault(item => item.ColumnOrder == column.Order);
                if (targetColumn is null) continue;
                var mappingMethod = mapping?.MappingMethod switch
                {
                    "AI" or "Automatic" => "AI",
                    "Manual" => "Manual",
                    _ => null
                };
                var isMapped = mappingMethod is not null && !string.IsNullOrWhiteSpace(mapping?.TargetColumn);
                sources.Add(new SourceColumn { ColumnId = targetColumn.ColumnId, SourceColumnName = column.Name, IsMappedToStandard = isMapped, MappedStandardColumn = isMapped ? mapping!.TargetColumn : null, IsSelectedMetadata = selected, MappingMethod = mappingMethod, ConfirmedByCustomer = isMapped && string.Equals(mappingMethod, "Manual", StringComparison.OrdinalIgnoreCase), CreatedOn = Now });
            }
        }
        database.SourceColumns.AddRange(sources);
        await database.SaveChangesAsync(token);
        await database.SaveChangesAsync(token);
        foreach (var sheet in sheets)
        {
            var parsedSheet = workbook.Sheets.FirstOrDefault(item => item.Order == sheet.SheetOrder);
            if (parsedSheet is null) continue;
            var selected = sources.Where(source => source.IsSelectedMetadata && sheet.Columns.Any(column => column.ColumnId == source.ColumnId))
                .Select(source => new { Source = source, ColumnOrder = parsedSheet.Columns.FirstOrDefault(column => string.Equals(column.Name, source.SourceColumnName, StringComparison.OrdinalIgnoreCase))?.Order ?? -1 })
                .Where(item => item.ColumnOrder >= 0).ToList();
            if (selected.Count == 0) continue;
            var parsedRows = parsedSheet.Rows.ToDictionary(row => row.Number);
            long lastRowId = 0;
            while (true)
            {
                var rows = await database.Rows.AsNoTracking().Where(row => row.SheetId == sheet.SheetId && row.RowId > lastRowId).OrderBy(row => row.RowId).Take(5000).ToListAsync(token);
                if (rows.Count == 0) break;
                var values = rows.Where(row => parsedRows.ContainsKey(row.RowNumber)).SelectMany(row => selected.Select(item => new { Row = row, item.Source, Value = parsedRows[row.RowNumber].Cells.FirstOrDefault(cell => cell.ColumnOrder == item.ColumnOrder)?.Value })).Where(item => !string.IsNullOrWhiteSpace(item.Value)).Select(item => new RowMetadataValue { RowId = item.Row.RowId, SourceColumnId = item.Source.SourceColumnId, Value = item.Value, DetectedDataType = "string", ValidationStatus = "Valid" }).ToList();
                if (values.Count > 0) { database.RowMetadataValues.AddRange(values); await database.SaveChangesAsync(token); }
                lastRowId = rows[^1].RowId;
            }
        }
    }
    public async Task<IReadOnlyList<Upload>> GetHistoryAsync(string userId, Token token) => await database.Uploads.AsNoTracking()
        // Customers can review their own inactive uploads as well as active uploads.
        .Where(x => x.User != null && x.User.UserName == userId).Include(x => x.User).Include(x => x.Sheets).Include(x => x.Transactions)
        .OrderByDescending(x => x.UploadedOn).ToListAsync(token);

    public async Task<IReadOnlyList<Upload>> GetAllHistoryAsync(Token token) => await database.Uploads.AsNoTracking()
        // Admin governance/history may list inactive records, but all data
        // access paths below still require an active upload for admin scope.
        .Include(x => x.User).Include(x => x.Sheets).Include(x => x.Transactions)
        .OrderByDescending(x => x.UploadedOn).ToListAsync(token);

    public Task<Upload?> GetOwnedAsync(string userId, long uploadId, Token token) => database.Uploads
        .Include(x => x.User).Include(x => x.Sheets).Include(x => x.Transactions).FirstOrDefaultAsync(x => x.UploadId == uploadId &&
            (userId == "*" || x.User != null && x.User.UserName == userId), token);

    public Task<IngestionSheet?> GetOwnedSheetAsync(string userId, long sheetId, Token token) => database.Sheets
        .Include(x => x.Columns).ThenInclude(column => column.SourceColumns).FirstOrDefaultAsync(x => x.SheetId == sheetId &&
            (userId == "*" || x.Upload!.User != null && x.Upload.User.UserName == userId), token);

    public async Task<IReadOnlyList<IngestionRow>> GetOwnedRowsAsync(string userId, long sheetId, int page, int pageSize, string? search, Token token)
    {
        var query = database.Rows.AsNoTracking().Include(x => x.Sheet).ThenInclude(sheet => sheet!.Columns).ThenInclude(column => column.SourceColumns).Include(x => x.MetadataValues).ThenInclude(value => value.SourceColumn)
            .Where(x => x.SheetId == sheetId &&
                (userId == "*" || x.Sheet!.Upload!.User != null && x.Sheet.Upload.User.UserName == userId));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => (x.Facility != null && x.Facility.Contains(search)) || (x.Manufacture != null && x.Manufacture.Contains(search)) || (x.Product != null && x.Product.Contains(search)) || (x.ProductDescription != null && x.ProductDescription.Contains(search)) || (x.CustomerUom != null && x.CustomerUom.Contains(search)) || x.MetadataValues.Any(value => value.Value != null && value.Value.Contains(search)));
        return await query.OrderBy(x => x.RowNumber).ThenBy(x => x.RowId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
    }

    public async Task<(long Uploads, long Rows, long Sheets, long Duplicates, long Nulls, long PreviousUploads, long PreviousRows)> GetDashboardAsync(string userId, Token token)
    {
        var today = Now.Date; var currentStart = today.AddDays(-30); var previousStart = today.AddDays(-60);
        // Dashboard metrics and trends contain active uploads only. Customers
        // can still access inactive files through their own history/data APIs.
        var uploads = database.Uploads.Where(x => x.User != null && x.User.UserName == userId);
        var current = uploads.Where(x => x.UploadedOn >= currentStart);
        var previous = uploads.Where(x => x.UploadedOn >= previousStart && x.UploadedOn < currentStart);
        return (await uploads.LongCountAsync(token), await uploads.SelectMany(x => x.Sheets).SelectMany(x => x.Rows).LongCountAsync(token),
            await uploads.SelectMany(x => x.Sheets).LongCountAsync(token), await uploads.SelectMany(x => x.Sheets).SelectMany(x => x.Rows).LongCountAsync(x => x.HasDuplicate, token),
            await uploads.SelectMany(x => x.Sheets).SelectMany(x => x.Rows).LongCountAsync(x => x.HasNull, token),
            await current.LongCountAsync(token), await current.SelectMany(x => x.Sheets).SelectMany(x => x.Rows).LongCountAsync(token));
    }

    public async Task<(long Uploads, long Active, long Inactive, long Rows, long Sheets, long Duplicates, long Nulls)> GetAdminDashboardAsync(Token token)
    {
        // Keep inactive uploads entirely outside the admin aggregate scope.
        var uploads = database.Uploads;
        return (await uploads.LongCountAsync(token), await uploads.LongCountAsync(token), 0,
            await uploads.SelectMany(x => x.Sheets).SelectMany(x => x.Rows).LongCountAsync(token), await uploads.SelectMany(x => x.Sheets).LongCountAsync(token),
            await uploads.SelectMany(x => x.Sheets).SelectMany(x => x.Rows).LongCountAsync(x => x.HasDuplicate, token), await uploads.SelectMany(x => x.Sheets).SelectMany(x => x.Rows).LongCountAsync(x => x.HasNull, token));
    }

    public async Task<IReadOnlyList<StandardColumnCoverageDto>> GetAdminStandardColumnCoverageAsync(Token token)
    {
        var rows = database.Uploads.AsNoTracking().SelectMany(upload => upload.Sheets).SelectMany(sheet => sheet.Rows);
        var aggregate = await rows.GroupBy(_ => 1).Select(group => new
        {
            Facility = group.LongCount(row => row.Facility != null && row.Facility != ""),
            Manufacture = group.LongCount(row => row.Manufacture != null && row.Manufacture != ""),
            Product = group.LongCount(row => row.Product != null && row.Product != ""),
            ProductDescription = group.LongCount(row => row.ProductDescription != null && row.ProductDescription != ""),
            CustomerUom = group.LongCount(row => row.CustomerUom != null && row.CustomerUom != ""),
            CustomerQty = group.LongCount(row => row.CustomerQty != null)
        }).SingleOrDefaultAsync(token);

        var total = await rows.LongCountAsync(token);
        return
        [
            Coverage("Facility", aggregate?.Facility ?? 0, total),
            Coverage("Manufacture", aggregate?.Manufacture ?? 0, total),
            Coverage("Product", aggregate?.Product ?? 0, total),
            Coverage("Product Description", aggregate?.ProductDescription ?? 0, total),
            Coverage("Customer UOM", aggregate?.CustomerUom ?? 0, total),
            Coverage("Customer Qty", aggregate?.CustomerQty ?? 0, total)
        ];

        static StandardColumnCoverageDto Coverage(string name, long filled, long totalRows) => new(name, filled, Math.Max(0, totalRows - filled));
    }

    public async Task<IReadOnlyList<AdminCustomerVolumeDto>> GetAdminCustomerVolumeAsync(Token token)
    {
        var activeUploads = from upload in database.Uploads.AsNoTracking()
                            join user in database.Users.AsNoTracking() on upload.UserId equals user.UserId
                            select new { upload, user.UserName };

        return await activeUploads
            .GroupBy(item => item.UserName)
            .Select(group => new AdminCustomerVolumeDto(
                group.Key,
                group.LongCount(),
                group.SelectMany(item => item.upload.Sheets).SelectMany(sheet => sheet.Rows).LongCount()))
            .OrderByDescending(item => item.Rows)
            .ThenBy(item => item.UserName)
            .ToListAsync(token);
    }

    public async Task<IReadOnlyList<StandardColumnCoverageDto>> GetStandardColumnCoverageAsync(string userId, long uploadId, Token token)
    {
        var uploads = database.Uploads.AsNoTracking().Where(upload => upload.UploadId == uploadId &&
            (userId == "*" || upload.User != null && upload.User.UserName == userId));
        var rows = uploads.SelectMany(upload => upload.Sheets).SelectMany(sheet => sheet.Rows);
        var aggregate = await rows.GroupBy(_ => 1).Select(group => new
        {
            Facility = group.LongCount(row => row.Facility != null && row.Facility != ""),
            Manufacture = group.LongCount(row => row.Manufacture != null && row.Manufacture != ""),
            Product = group.LongCount(row => row.Product != null && row.Product != ""),
            ProductDescription = group.LongCount(row => row.ProductDescription != null && row.ProductDescription != ""),
            CustomerUom = group.LongCount(row => row.CustomerUom != null && row.CustomerUom != ""),
            CustomerQty = group.LongCount(row => row.CustomerQty != null)
        }).SingleOrDefaultAsync(token);
        var total = await rows.LongCountAsync(token);
        return
        [
            Coverage("Facility", aggregate?.Facility ?? 0, total),
            Coverage("Manufacture", aggregate?.Manufacture ?? 0, total),
            Coverage("Product", aggregate?.Product ?? 0, total),
            Coverage("Product Description", aggregate?.ProductDescription ?? 0, total),
            Coverage("Customer UOM", aggregate?.CustomerUom ?? 0, total),
            Coverage("Customer Qty", aggregate?.CustomerQty ?? 0, total)
        ];

        static StandardColumnCoverageDto Coverage(string name, long filled, long totalRows) => new(name, filled, Math.Max(0, totalRows - filled));
    }

    public async Task<IReadOnlyList<(string UserName, DateTime UploadedOn)>> GetDailyUploadEventsAsync(string? userName, Token token)
    {
        var start = Now.Date.AddDays(-29);
        // Daily upload analysis excludes inactive uploads for both admin and customer views.
        var query = database.Uploads.AsNoTracking().Where(upload => upload.UploadedOn >= start);
        if (!string.IsNullOrWhiteSpace(userName)) query = query.Where(upload => upload.User != null && upload.User.UserName == userName);
        return await query.Select(upload => new ValueTuple<string, DateTime>(upload.User!.UserName, upload.UploadedOn)).ToListAsync(token);
    }
}
