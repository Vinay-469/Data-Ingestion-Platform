using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using CoreModels.Entities;
using CoreModels.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace BusinessLayer.Services;

public sealed class UploadService(
    IUploadRepository repository,
    IEnumerable<IFileParser> parsers,
    IFileStorage storage,
    IFileValidationService validation,
    ILogger<UploadService> logger,
    IPendingUploadStore pendingStore,
    INotificationPublisher notifications,
    IOptions<ApplicationSettings> settings) : IUploadService
{
    private readonly IPendingUploadStore pendingUploads = pendingStore;
    private readonly ApplicationSettings applicationSettings = settings.Value;
    private DateTime Now => DateTime.UtcNow.AddHours(applicationSettings.Time.IndiaStandardTimeOffsetHours);

    public async Task<ValidationSummaryDto> UploadAsync(string userId, IFormFile file, Token token)
    {
        validation.Validate(file);
        var ownerId = await repository.GetUserIdAsync(userId, token) ?? throw new UnauthorizedAccessException("The authenticated user could not be found.");
        await using var source = file.OpenReadStream();
        var storagePath = await storage.SaveAsync(userId, file.FileName, source, token);
        var parser = parsers.FirstOrDefault(item => item.CanParse(Path.GetExtension(file.FileName).ToLowerInvariant())) ?? throw new InvalidDataException("No parser is registered for this file type.");
        ParsedWorkbook workbook;
        await using (var input = await storage.OpenReadAsync(storagePath, token) ?? throw new FileNotFoundException("Stored file could not be opened."))
        {
            workbook = await parser.ParseAsync(input, file.FileName, token);
        }
        await storage.PromoteAsync(storagePath, userId, file.FileName, token);
        var upload = new Upload { UserId = ownerId, OriginalFileName = Path.GetFileName(file.FileName), Status = "Completed", UploadedOn = Now };
        string? remarks = null;
        var transaction = new UploadTransaction { StartedAt = Now, Status = "Completed", TotalSheets = workbook.Sheets.Count };
        var duplicateRows = 0L; var nullRows = 0L; var totalRows = 0L; var totalColumns = 0;

        foreach (var parsedSheet in workbook.Sheets)
        {
            var sheet = new IngestionSheet { SheetName = parsedSheet.Name, SheetOrder = parsedSheet.Order, TotalRows = parsedSheet.Rows.Count, TotalColumns = parsedSheet.Columns.Count };
            var columns = parsedSheet.Columns.Select(column => new IngestionColumn { ColumnOrder = column.Order, AllowNull = true }).ToList();
            foreach (var column in columns) sheet.Columns.Add(column);
            var seenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in parsedSheet.Columns) { if (!seenHeaders.Add(column.Name.Trim())) remarks = "Duplicate column names detected."; }
            var signatures = new HashSet<string>();
            foreach (var parsedRow in parsedSheet.Rows)
            {
                var values = parsedRow.Cells.Select(cell => cell.Value ?? string.Empty).ToList();
                var hasNull = values.Any(string.IsNullOrWhiteSpace); var duplicate = !signatures.Add(string.Join("\u001F", values));
                if (duplicate) duplicateRows++; if (hasNull) nullRows++; totalRows++;
                var row = new IngestionRow { RowNumber = parsedRow.Number, HasDuplicate = duplicate, HasNull = hasNull, IsValid = !duplicate && !hasNull, CreatedOn = Now };
                ApplyStandardFields(row, parsedSheet, parsedRow);
                sheet.Rows.Add(row);
            }
            upload.Sheets.Add(sheet); totalColumns += columns.Count;
        }
        transaction.TotalRows = totalRows; transaction.TotalColumns = totalColumns; transaction.DuplicateRows = duplicateRows; transaction.NullRows = nullRows; upload.Transactions.Add(transaction);
        await PersistBatchAsync(upload, token);
        await TryPublishUploadNotificationsAsync(userId, upload, "uploaded successfully", "was uploaded by", token);
        logger.LogInformation("Upload completed for user {UserId}, file {FileName}, rows {Rows}, sheets {Sheets}", userId, file.FileName, totalRows, workbook.Sheets.Count);
        return new ValidationSummaryDto(upload.UploadId, upload.Status, totalRows, duplicateRows, nullRows, 0, workbook.Sheets.Count, remarks);
    }

    public async Task<UploadValidationResultDto> ValidateAsync(string userId, IFormFile file, Token token)
    {
        validation.Validate(file);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var parser = parsers.FirstOrDefault(item => item.CanParse(extension)) ?? throw new InvalidDataException("No parser is registered for this file type.");
        await using var source = file.OpenReadStream();
        var storagePath = await storage.SaveAsync(userId, file.FileName, source, token);

        try
        {
            await using var input = await storage.OpenReadAsync(storagePath, token) ?? throw new FileNotFoundException("Temporary upload could not be opened.");
            var workbook = await parser.ParseAsync(input, file.FileName, token);
            var history = await repository.GetHistoryAsync(userId, token);
            var duplicateFile = history.Any(item => string.Equals(item.OriginalFileName, Path.GetFileName(file.FileName), StringComparison.OrdinalIgnoreCase));
            var sheetNames = workbook.Sheets.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Sum(group => group.Count() - 1);
            var duplicateColumns = workbook.Sheets.Sum(sheet => sheet.Columns.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1).Sum(group => group.Count() - 1));
            var emptySheets = workbook.Sheets.Count(sheet => sheet.Columns.Count == 0 || sheet.Rows.Count == 0);
            var totalRows = workbook.Sheets.Sum(sheet => (long)sheet.Rows.Count);
            var totalColumns = workbook.Sheets.Sum(sheet => sheet.Columns.Count);
            var duplicateRows = 0L;
            var nullRows = 0L;
            var invalidTypes = 0L;
            foreach (var sheet in workbook.Sheets)
            {
                var signatures = new HashSet<string>();
                foreach (var row in sheet.Rows)
                {
                    var values = row.Cells.Select(cell => cell.Value ?? string.Empty).ToList();
                    if (!signatures.Add(string.Join("\u001F", values))) duplicateRows++;
                    if (values.Any(string.IsNullOrWhiteSpace)) nullRows++;
                }

                for (var columnIndex = 0; columnIndex < sheet.Columns.Count; columnIndex++)
                {
                    var types = sheet.Rows.Select(row => row.Cells.FirstOrDefault(cell => cell.ColumnOrder == columnIndex)?.DataType).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (types.Count > 1) invalidTypes += sheet.Rows.LongCount(row => row.Cells.FirstOrDefault(cell => cell.ColumnOrder == columnIndex)?.Value is not null);
                }
            }

            var emptyWorkbook = workbook.Sheets.Count == 0;
            var issues = new List<string>();
            var missingStandardColumns = RequiredStandardColumns.Where(required => !workbook.Sheets.Any(sheet => sheet.Columns.Any(column => StandardColumns.TryGetValue(HeaderKey(column.Name), out var standard) && HeaderKey(standard) == required.Key))).Select(required => required.Label).ToList();
            if (missingStandardColumns.Count > 0) issues.Add($"Required standard columns are missing: {string.Join(", ", missingStandardColumns)}.");
            var additionalColumns = workbook.Sheets.SelectMany(sheet => sheet.Columns).Where(column => !StandardColumns.ContainsKey(HeaderKey(column.Name))).Select(column => column.Name.Trim()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
            if (emptyWorkbook) issues.Add("The workbook does not contain any sheets.");
            if (emptySheets > 0) issues.Add($"{emptySheets} sheet(s) are empty.");
            if (duplicateFile) issues.Add("A file with the same name already exists for this user.");
            if (sheetNames > 0) issues.Add("Duplicate sheet names were found.");
            if (duplicateColumns > 0) issues.Add("Duplicate column names were found.");
            if (duplicateRows > 0) issues.Add("Duplicate rows were found.");
            if (nullRows > 0) issues.Add("Rows with blank values were found.");
            if (invalidTypes > 0) issues.Add("Mixed datatypes were found in one or more columns.");

            var validationId = Guid.NewGuid();
            var metrics = new UploadValidationMetrics(workbook.Sheets.Count, totalRows, totalColumns, duplicateRows, nullRows, duplicateFile, sheetNames, duplicateColumns, emptySheets, emptyWorkbook, invalidTypes, issues, additionalColumns);
            var expiresAt = Now.AddMinutes(applicationSettings.Upload.ValidationExpiryMinutes);
            pendingUploads.Add(new PendingUpload(validationId, userId, Path.GetFileName(file.FileName), extension, storagePath, workbook, expiresAt, metrics));
            var sourceColumns = workbook.Sheets.OrderBy(sheet => sheet.Order).SelectMany(sheet => sheet.Columns.OrderBy(column => column.Order).Select(column => new SourceColumnDto(sheet.Name, sheet.Order, column.Name, column.Order, "string"))).ToList();
            return new UploadValidationResultDto(validationId, Path.GetFileName(file.FileName), "AwaitingConfirmation", expiresAt, metrics.TotalSheets, metrics.TotalRows, metrics.TotalColumns, metrics.DuplicateRows, metrics.NullRows, metrics.DuplicateFile, metrics.DuplicateSheetNames, metrics.DuplicateColumns, metrics.EmptySheets, metrics.EmptyWorkbook, metrics.InvalidDataTypeCells, metrics.Issues, metrics.AdditionalColumns, sourceColumns);
        }
        catch
        {
            await storage.DeleteAsync(storagePath, token);
            throw;
        }
    }

    public async Task<ValidationSummaryDto?> ConfirmAsync(string userId, UploadConfirmationRequest request, Token token, IProgress<int>? progress = null)
    {
        if (!pendingUploads.TryGet(request.ValidationId, out var pending) || pending is null || !string.Equals(pending.UserId, userId, StringComparison.Ordinal)) return null;
        if (pending.Metrics.DuplicateFile) throw new InvalidDataException("A file with the same name already exists for this user.");
        var excludedSheetOrders = request.ExcludedSheetOrders?.ToHashSet() ?? [];
        var selectedSheets = pending.Workbook.Sheets.Where(sheet => !excludedSheetOrders.Contains(sheet.Order)).ToList();
        if (selectedSheets.Count == 0) throw new InvalidDataException("Select at least one sheet before importing the workbook.");
        if (request.Mappings is not null)
        {
            // The header name is the stable identity supplied by the workbook.
            // Normalize the order sent by older/AI clients before validating so
            // a one-based order cannot make an otherwise valid mapping fail.
            request = request with
            {
                Mappings = NormalizeConfirmedMappings(pending.Workbook, request.Mappings).Where(mapping => !excludedSheetOrders.Contains(mapping.SheetOrder)).ToList(),
                SelectedMetadataColumns = request.SelectedMetadataColumns is null
                    ? null
                    : NormalizeMetadataSelections(pending.Workbook, request.SelectedMetadataColumns).Where(selection => !excludedSheetOrders.Contains(selection.SheetOrder)).ToList()
            };
            ValidateConfirmedMappings(selectedSheets, request.Mappings, request.SelectedMetadataColumns);
        }
        if (pending.Metrics.NullRows > 0 && !request.KeepNullRows) { }
        if (pending.Metrics.DuplicateRows > 0 && !request.SkipDuplicateRows) { }
        if (pending.Metrics.InvalidDataTypeCells > 0 && !request.SkipInvalidRows) { }

            var ownerId = await repository.GetUserIdAsync(userId, token) ?? throw new UnauthorizedAccessException("The authenticated user could not be found.");
            await storage.PromoteAsync(pending.StoragePath, userId, pending.OriginalFileName, token);
            var upload = new Upload { UserId = ownerId, OriginalFileName = pending.OriginalFileName, Status = "Completed", UploadedOn = Now };
        var transaction = new UploadTransaction { StartedAt = Now, Status = "Completed", TotalSheets = selectedSheets.Count };
        var skippedRows = 0L;
        var persistedRows = 0L;
        var totalRowsToProcess = Math.Max(1L, selectedSheets.Sum(sheet => (long)sheet.Rows.Count));
        var duplicateAudits = new List<DuplicateAudit>();
        var validationAudits = new List<ValidationAudit>();
        var duplicateRows = 0L;
        var nullRows = 0L;
        // Match the repository's staged bulk-copy size so confirmation uses a
        // small number of SQL Server round-trips instead of one call per
        // 5,000 rows.
        const int rowBatchSize = 25_000;
        const int maxValidationAuditRows = 10_000;

        // Create only workbook metadata up front. Row entities are created and
        // released in bounded batches below; this avoids duplicating a million
        // parsed rows in the EF entity graph.
        foreach (var parsedSheet in selectedSheets)
        {
            var sheet = new IngestionSheet { SheetName = parsedSheet.Name, SheetOrder = parsedSheet.Order, TotalRows = parsedSheet.Rows.Count, TotalColumns = parsedSheet.Columns.Count };
            var columns = parsedSheet.Columns.Select(column => new IngestionColumn { ColumnOrder = column.Order, AllowNull = true }).ToList();
            foreach (var column in columns) sheet.Columns.Add(column);
            upload.Sheets.Add(sheet);
        }

        for (var sheetIndex = 0; sheetIndex < selectedSheets.Count; sheetIndex++)
        {
            var parsedSheet = selectedSheets[sheetIndex];
            var sheet = upload.Sheets.ElementAt(sheetIndex);
            // Always calculate duplicate rows so the database flags and analysis
            // remain accurate. The option only controls whether duplicates are
            // skipped during import, not whether they are detected.
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parsedBatch in parsedSheet.Rows.Chunk(rowBatchSize))
            {
                foreach (var parsedRow in parsedBatch)
                {
                    var values = parsedRow.Cells.Select(cell => cell.Value ?? string.Empty).ToList();
                    var hasNull = values.Any(string.IsNullOrWhiteSpace);
                    var duplicate = !signatures.Add(string.Join("\u001F", values));
                    if (duplicate) duplicateRows++;
                    if (hasNull) nullRows++;
                    if ((duplicate && request.SkipDuplicateRows) || (hasNull && !request.KeepNullRows))
                    {
                        skippedRows++;
                        if (validationAudits.Count < maxValidationAuditRows)
                            validationAudits.Add(new ValidationAudit { UploadId = 0, SheetId = 0, RowId = 0, ValidationType = duplicate ? "DuplicateRowSkipped" : "NullRowSkipped", ValidationMessage = $"Row {parsedRow.Number} was skipped during confirmation.", CreatedOn = Now });
                        continue;
                    }
                    var row = new IngestionRow { RowNumber = parsedRow.Number, HasDuplicate = duplicate, HasNull = hasNull, IsValid = !duplicate && !hasNull, CreatedOn = Now };
                    if (request.Mappings is null) ApplyStandardFields(row, parsedSheet, parsedRow);
                    else ApplyMappedFields(row, parsedSheet, parsedRow, request.Mappings);
                    sheet.Rows.Add(row);
                    persistedRows++;
                }

                // Persist and clear this batch before creating the next batch.
                await PersistBatchAsync(upload, token);
                progress?.Report(5 + (int)Math.Min(90, persistedRows * 90 / totalRowsToProcess));
            }
        }
        transaction.TotalRows = persistedRows; transaction.TotalColumns = upload.Sheets.Sum(sheet => sheet.Columns.Count); transaction.DuplicateRows = duplicateRows; transaction.NullRows = nullRows; transaction.SkippedRows = skippedRows; upload.Transactions.Add(transaction);
        // The row collections were cleared after each batch. This final call
        // writes the transaction without sending another row payload.
        await PersistBatchAsync(upload, token);
        if (request.Mappings is not null || request.SelectedMetadataColumns is not null) await repository.PersistColumnMetadataAsync(upload.UploadId, pending.Workbook, request, token);
        foreach (var audit in validationAudits) audit.UploadId = upload.UploadId;
        await repository.AddAuditsAsync(duplicateAudits, validationAudits, token);
        await repository.SaveChangesAsync(token);
        await TryPublishUploadNotificationsAsync(userId, upload, "imported successfully", "was imported by", token);
        pendingUploads.Remove(request.ValidationId, out _);
        logger.LogInformation("Upload confirmed for user {UserId}, file {FileName}, rows {Rows}, skipped {SkippedRows}", userId, pending.OriginalFileName, transaction.TotalRows, skippedRows);
        return new ValidationSummaryDto(upload.UploadId, upload.Status, transaction.TotalRows, transaction.DuplicateRows, transaction.NullRows, skippedRows, transaction.TotalSheets, null);
    }

    public async Task<bool> CancelValidationAsync(string userId, Guid validationId, Token token)
    {
        if (!pendingUploads.TryGet(validationId, out var pending) || pending is null || !string.Equals(pending.UserId, userId, StringComparison.Ordinal)) return false;
        pendingUploads.Remove(validationId, out _);
        await storage.DeleteAsync(pending.StoragePath, token);
        logger.LogInformation("Upload validation cancelled for user {UserId}, file {FileName}", userId, pending.OriginalFileName);
        return true;
    }

    public async Task<IReadOnlyList<UploadSummaryDto>> GetHistoryAsync(string userId, Token token) => (await repository.GetHistoryAsync(userId, token)).Select(upload => new UploadSummaryDto(upload.UploadId, upload.User?.UserName ?? userId, upload.OriginalFileName, upload.Status, true, upload.UploadedOn, upload.Sheets.Count, upload.Sheets.Sum(sheet => sheet.TotalRows), upload.Transactions.Sum(transaction => transaction.DuplicateRows), upload.Transactions.Sum(transaction => transaction.NullRows))).ToList();
    public async Task<IReadOnlyList<UploadSummaryDto>> GetAllHistoryAsync(Token token) => (await repository.GetAllHistoryAsync(token)).Select(upload => new UploadSummaryDto(upload.UploadId, upload.User?.UserName ?? string.Empty, upload.OriginalFileName, upload.Status, true, upload.UploadedOn, upload.Sheets.Count, upload.Sheets.Sum(sheet => sheet.TotalRows), upload.Transactions.Sum(transaction => transaction.DuplicateRows), upload.Transactions.Sum(transaction => transaction.NullRows))).ToList();
    public async Task<IReadOnlyList<SheetDto>> GetSheetsAsync(string userId, long uploadId, Token token) => (await repository.GetOwnedAsync(userId, uploadId, token))?.Sheets.OrderBy(sheet => sheet.SheetOrder).Select(sheet => new SheetDto(sheet.SheetId, sheet.SheetName, sheet.SheetOrder, sheet.TotalRows, sheet.TotalColumns)).ToList() ?? [];
    public async Task<IReadOnlyList<ColumnDto>> GetColumnsAsync(string userId, long sheetId, Token token) { var sheet = await repository.GetOwnedSheetAsync(userId, sheetId, token); return sheet?.Columns.OrderBy(column => column.ColumnOrder).Select(column => { var name = column.SourceColumns.FirstOrDefault()?.SourceColumnName ?? $"Column{column.ColumnOrder + 1}"; return new ColumnDto(column.ColumnId, name, name, column.ColumnOrder, column.DetectedDataType, column.AllowNull, column.UniqueValues); }).ToList() ?? []; }
    public async Task<IReadOnlyList<RowDto>> GetRowsAsync(string userId, long sheetId, int page, int pageSize, string? search, Token token) => (await repository.GetOwnedRowsAsync(userId, sheetId, Math.Max(page, 1), Math.Clamp(pageSize, 1, 500), search, token)).Select(row => new RowDto(row.RowId, row.RowNumber, row.HasDuplicate, row.HasNull, row.IsValid, row.Facility, row.Manufacture, row.Product, row.ProductDescription, row.CustomerUom, row.CustomerQty, BuildCells(row))).ToList();

    private static IReadOnlyList<CellDto> BuildCells(IngestionRow row)
    {
        var metadata = row.MetadataValues.Where(value => value.SourceColumn is not null).ToDictionary(value => value.SourceColumn!.SourceColumnId, value => value.Value);
        return row.Sheet?.Columns.OrderBy(column => column.ColumnOrder).Select(column =>
        {
            var source = column.SourceColumns.FirstOrDefault();
            var target = source?.MappedStandardColumn;
            var value = target switch
            {
                "Facility" => row.Facility,
                "Manufacture" => row.Manufacture,
                "Product" => row.Product,
                "Product Description" => row.ProductDescription,
                "Customer UOM" => row.CustomerUom,
                "Customer Qty" => row.CustomerQty?.ToString(CultureInfo.InvariantCulture),
                _ when source is not null && metadata.TryGetValue(source.SourceColumnId, out var metadataValue) => metadataValue,
                _ => null
            };
            return new CellDto(column.ColumnId, source?.SourceColumnName ?? $"Column{column.ColumnOrder + 1}", value, column.DetectedDataType, string.IsNullOrWhiteSpace(value) ? "Null" : "Valid");
        }).ToList() ?? [];
    }
    public async Task<Stream?> DownloadAsync(string userId, long uploadId, Token token) { var upload = await repository.GetOwnedAsync(userId, uploadId, token); return upload is null ? null : await storage.OpenReadAsync(storage.GetPath(upload.User?.UserName ?? userId, upload.OriginalFileName), token); }
    public async Task<DashboardDto> GetDashboardAsync(string userId, Token token) { var data = await repository.GetDashboardAsync(userId, token); var totalRows = data.Rows == 0 ? 1 : data.Rows; var events = await repository.GetDailyUploadEventsAsync(userId, token); return new DashboardDto(data.Uploads, data.Uploads, data.Rows, data.Sheets, data.Duplicates, data.Nulls, Math.Max(0, (double)(data.Rows - data.Duplicates - data.Nulls) / totalRows * 100), Math.Min(100, (double)(data.Duplicates + data.Nulls) / totalRows * 100), CalculateTrend(data.Uploads, data.PreviousUploads), CalculateTrend(data.Rows, data.PreviousRows), BuildDailySeries(events, userId)); }
    public async Task<AdminDashboardDto> GetAdminDashboardAsync(long totalUsers, Token token)
    {
        var data = await repository.GetAdminDashboardAsync(token);
        var totalRows = data.Rows == 0 ? 1 : data.Rows;
        var recent = await GetAllHistoryAsync(token);
        var events = await repository.GetDailyUploadEventsAsync(null, token);
        IReadOnlyList<StandardColumnCoverageDto> coverage = [];
        IReadOnlyList<AdminCustomerVolumeDto> customerVolume = [];

        try { coverage = await repository.GetAdminStandardColumnCoverageAsync(token); }
        catch (Exception exception) { logger.LogWarning(exception, "Admin standard-column analytics could not be loaded."); }

        try { customerVolume = await repository.GetAdminCustomerVolumeAsync(token); }
        catch (Exception exception) { logger.LogWarning(exception, "Admin customer-volume analytics could not be loaded."); }
        if (customerVolume.Count == 0)
        {
            customerVolume = recent
                .Where(item => !string.IsNullOrWhiteSpace(item.UserName))
                .GroupBy(item => item.UserName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AdminCustomerVolumeDto(group.Key, group.LongCount(), group.Sum(item => item.TotalRows)))
                .OrderByDescending(item => item.Rows)
                .ThenBy(item => item.UserName)
                .ToList();
        }

        return new AdminDashboardDto(totalUsers, data.Uploads, data.Active, data.Inactive, data.Rows, data.Sheets, data.Duplicates, data.Nulls, Math.Max(0, (double)(data.Rows - data.Duplicates - data.Nulls) / totalRows * 100), recent.Take(10).ToList(), BuildAdminDailySeries(events), coverage, customerVolume);
    }
    public Task<IReadOnlyList<StandardColumnCoverageDto>> GetStandardColumnCoverageAsync(string userId, long uploadId, Token token) => repository.GetStandardColumnCoverageAsync(userId, uploadId, token);
    private async Task PersistBatchAsync(Upload upload, Token token)
    {
        try { await repository.BulkPersistAsync(upload, token); }
        catch
        {
            if (upload.UploadId > 0) { try { await repository.DeleteUploadAsync(upload.UploadId, Token.None); } catch (Exception cleanupException) { logger.LogError(cleanupException, "Failed to clean up cancelled upload {UploadId}", upload.UploadId); } }
            throw;
        }
    }

    public async Task<bool> SetStatusAsync(string userId, long uploadId, bool isActive, Token token)
    {
        var upload = await repository.GetOwnedAsync(userId, uploadId, token);
        if (upload is null) return false;
        var fileName = upload.OriginalFileName;
        var ownerName = upload.User?.UserName ?? userId;
        await storage.DeleteAsync(storage.GetPath(ownerName, fileName), token);
        await repository.DeleteUploadAsync(uploadId, token);
        var messages = applicationSettings.Notifications;
        await notifications.PublishToUserAsync(userId, new NotificationMessage("upload-deleted", messages.FileStateTitle, messages.FileStateMessage, uploadId, fileName, userId, Now), token);
        await notifications.PublishToAdministratorsAsync(new NotificationMessage("upload-deleted", messages.CustomerFileStateTitle, messages.CustomerFileStateMessage, uploadId, fileName, userId, Now), token);
        return true;
    }

    private async Task TryPublishUploadNotificationsAsync(string userId, Upload upload, string customerMessage, string adminMessagePrefix, Token token)
    {
        // The upload and its database transaction have already completed at this
        // point. SignalR is a secondary channel and must never turn a successful
        // import into a 500 response when a client disconnects or the hub is unavailable.
        try
        {
            var messages = applicationSettings.Notifications;
            await notifications.PublishToUserAsync(userId, new NotificationMessage("upload-completed", messages.UploadCompletedTitle, messages.UploadCompletedMessage, upload.UploadId, upload.OriginalFileName, userId, Now), Token.None);
            await notifications.PublishToAdministratorsAsync(new NotificationMessage("upload-completed", messages.NewCustomerUploadTitle, messages.NewCustomerUploadMessage, upload.UploadId, upload.OriginalFileName, userId, Now), Token.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Upload {UploadId} completed, but real-time notification delivery failed.", upload.UploadId);
        }
    }

    private static double? CalculateTrend(long current, long previous) => previous == 0 ? null : Math.Round((double)(current - previous) / previous * 100, 1);
    private static readonly IReadOnlyDictionary<string, string> StandardColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["facility"] = "Facility", ["plant"] = "Facility", ["location"] = "Facility", ["site"] = "Facility", ["sitename"] = "Facility", ["facilityname"] = "Facility", ["plantlocation"] = "Facility", ["operatingsite"] = "Facility", ["factory"] = "Facility",
        ["manufacture"] = "Manufacture", ["manufacturer"] = "Manufacture", ["maker"] = "Manufacture", ["producer"] = "Manufacture", ["supplier"] = "Manufacture", ["vendor"] = "Manufacture", ["vendormanufacturer"] = "Manufacture", ["producername"] = "Manufacture", ["brandmanufacturer"] = "Manufacture",
        ["product"] = "Product", ["productid"] = "Product", ["productcode"] = "Product", ["itemcode"] = "Product", ["sku"] = "Product", ["skunumber"] = "Product", ["articlenumber"] = "Product", ["materialnumber"] = "Product", ["materialcode"] = "Product",
        ["productdescription"] = "Product Description", ["description"] = "Product Description", ["itemdetails"] = "Product Description", ["itemdescription"] = "Product Description", ["productdetails"] = "Product Description", ["articledetails"] = "Product Description", ["skudescription"] = "Product Description", ["materialdescription"] = "Product Description", ["materialdetails"] = "Product Description",
        ["customeruom"] = "Customer UOM", ["uom"] = "Customer UOM", ["unitofmeasure"] = "Customer UOM", ["measureunit"] = "Customer UOM", ["unittype"] = "Customer UOM", ["customerunit"] = "Customer UOM", ["quantityunit"] = "Customer UOM", ["usageunit"] = "Customer UOM",
        ["customerqty"] = "Customer Qty", ["customerquantity"] = "Customer Qty", ["quantity"] = "Customer Qty", ["qty"] = "Customer Qty", ["orderquantity"] = "Customer Qty", ["requiredquantity"] = "Customer Qty", ["requiredqty"] = "Customer Qty", ["purchaseqty"] = "Customer Qty", ["demandquantity"] = "Customer Qty", ["customervolume"] = "Customer Qty", ["requiredamount"] = "Customer Qty"
    };
    private static readonly IReadOnlyList<(string Key, string Label)> RequiredStandardColumns = [("facility", "Facility"), ("manufacture", "Manufacture"), ("product", "Product"), ("productdescription", "Product Description"), ("customeruom", "Customer UOM"), ("customerqty", "Customer Qty")];
    private static string HeaderKey(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    private static void ApplyStandardFields(IngestionRow row, ParsedSheet sheet, ParsedRow parsedRow)
    {
        var values = parsedRow.Cells.ToDictionary(cell => cell.ColumnOrder, cell => cell.Value);
        var headers = sheet.Columns.ToDictionary(column => column.Order, column => column.Name);
        string? ValueFor(string key)
        {
            var match = headers.FirstOrDefault(item => StandardColumns.TryGetValue(HeaderKey(item.Value), out var standard) && HeaderKey(standard) == key);
            return string.IsNullOrWhiteSpace(match.Value) || !values.TryGetValue(match.Key, out var value) ? null : value;
        }
        row.Facility = ValueFor("facility"); row.Manufacture = ValueFor("manufacture") ?? ValueFor("manufacturer"); row.Product = ValueFor("product"); row.ProductDescription = ValueFor("productdescription"); row.CustomerUom = ValueFor("customeruom");
        if (decimal.TryParse(ValueFor("customerqty"), NumberStyles.Any, CultureInfo.InvariantCulture, out var quantity)) row.CustomerQty = quantity;
    }
    private static void ValidateConfirmedMappings(IReadOnlyList<ParsedSheet> sheets, IReadOnlyList<ConfirmedColumnMappingDto> mappings, IReadOnlyList<MetadataColumnSelectionDto>? metadata)
    {
        var targets = RequiredStandardColumns.Select(item => item.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in sheets)
        {
            var sheetMappings = mappings.Where(item => item.SheetOrder == sheet.Order).ToList();
            foreach (var target in targets) if (!sheetMappings.Any(item => string.Equals(item.TargetColumn, target, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException($"Please map the required field: {target} for sheet {sheet.Name}.");
            if (sheetMappings.Select(item => item.TargetColumn).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sheetMappings.Count) throw new InvalidDataException($"A source mapping is duplicated in sheet {sheet.Name}.");
            // ColumnOrder is the workbook's stable source-column identity. The
            // displayed source name is metadata from the workbook and may be
            // normalized by the AI/frontend, so it must not be used as the
            // authority for validation. TargetColumn is the canonical database
            // field name and is validated separately above.
            if (sheetMappings.Any(item => !string.Equals(item.MappingMethod, "Null", StringComparison.OrdinalIgnoreCase) && !sheet.Columns.Any(column => column.Order == item.ColumnOrder))) throw new InvalidDataException($"A selected source column does not exist in sheet {sheet.Name}. Please select the source column again.");
            if (metadata?.Where(item => item.SheetOrder == sheet.Order).Any(item => sheetMappings.Any(mapping => mapping.ColumnOrder == item.ColumnOrder)) == true) throw new InvalidDataException($"A mapped standard column cannot also be selected as metadata in sheet {sheet.Name}.");
        }
        var selectedSourceMappings = mappings.Where(item => !string.Equals(item.MappingMethod, "Null", StringComparison.OrdinalIgnoreCase)).ToList();
        if (selectedSourceMappings.Select(item => $"{item.SheetOrder}:{item.ColumnOrder}").Distinct().Count() != selectedSourceMappings.Count) throw new InvalidDataException("A source column cannot be mapped more than once in the same sheet.");
    }

    private static IReadOnlyList<ConfirmedColumnMappingDto> NormalizeConfirmedMappings(ParsedWorkbook workbook, IReadOnlyList<ConfirmedColumnMappingDto> mappings)
    {
        return mappings.Select(mapping =>
        {
            var sheet = ResolveSheet(workbook, mapping.SheetOrder, mapping.SheetName);
            if (sheet is null) throw new InvalidDataException($"The selected sheet '{mapping.SheetName}' does not exist in the workbook.");
            if (string.Equals(mapping.MappingMethod, "Null", StringComparison.OrdinalIgnoreCase))
                return mapping with { SheetName = sheet.Name, SheetOrder = sheet.Order, SourceColumnName = string.Empty, ColumnOrder = -1 };

            var source = ResolveColumn(sheet, mapping.ColumnOrder, mapping.SourceColumnName);
            if (source is null) throw new InvalidDataException($"The selected source column '{mapping.SourceColumnName}' does not exist in sheet {sheet.Name}. Please select the source column again.");
            return mapping with { SheetName = sheet.Name, SheetOrder = sheet.Order, SourceColumnName = source.Name, ColumnOrder = source.Order };
        }).ToList();
    }

    private static IReadOnlyList<MetadataColumnSelectionDto> NormalizeMetadataSelections(ParsedWorkbook workbook, IReadOnlyList<MetadataColumnSelectionDto> selections)
    {
        return selections.Select(selection =>
        {
            var sheet = ResolveSheet(workbook, selection.SheetOrder, selection.SheetName);
            if (sheet is null) throw new InvalidDataException($"The selected sheet '{selection.SheetName}' does not exist in the workbook.");
            var source = ResolveColumn(sheet, selection.ColumnOrder, selection.SourceColumnName);
            if (source is null) throw new InvalidDataException($"The selected metadata column '{selection.SourceColumnName}' does not exist in sheet {sheet.Name}.");
            return selection with { SheetName = sheet.Name, SheetOrder = sheet.Order, SourceColumnName = source.Name, ColumnOrder = source.Order };
        }).ToList();
    }

    private static ParsedSheet? ResolveSheet(ParsedWorkbook workbook, int sheetOrder, string sheetName) =>
        workbook.Sheets.FirstOrDefault(sheet => sheet.Order == sheetOrder) ??
        workbook.Sheets.FirstOrDefault(sheet => string.Equals(sheet.Name.Trim(), sheetName.Trim(), StringComparison.OrdinalIgnoreCase));

    private static ParsedColumn? ResolveColumn(ParsedSheet sheet, int columnOrder, string sourceColumnName)
    {
        var named = sheet.Columns.Where(column => !string.IsNullOrWhiteSpace(sourceColumnName) && string.Equals(column.Name.Trim(), sourceColumnName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (named.Count == 1) return named[0];
        var exact = sheet.Columns.FirstOrDefault(column => column.Order == columnOrder);
        if (exact is not null && (string.IsNullOrWhiteSpace(sourceColumnName) || string.Equals(exact.Name.Trim(), sourceColumnName.Trim(), StringComparison.OrdinalIgnoreCase))) return exact;
        // Accept a one-based order from older clients only when the displayed
        // source name confirms the same workbook column.
        var oneBased = sheet.Columns.FirstOrDefault(column => column.Order == columnOrder - 1);
        return oneBased is not null && (string.IsNullOrWhiteSpace(sourceColumnName) || string.Equals(oneBased.Name.Trim(), sourceColumnName.Trim(), StringComparison.OrdinalIgnoreCase)) ? oneBased : null;
    }
    private static void ApplyMappedFields(IngestionRow row, ParsedSheet sheet, ParsedRow parsedRow, IReadOnlyList<ConfirmedColumnMappingDto> mappings)
    {
        var values = parsedRow.Cells.ToDictionary(cell => cell.ColumnOrder, cell => cell.Value);
        string? ValueFor(string target) { var mapping = mappings.FirstOrDefault(item => item.SheetOrder == sheet.Order && string.Equals(item.TargetColumn, target, StringComparison.OrdinalIgnoreCase)); return mapping is not null && values.TryGetValue(mapping.ColumnOrder, out var value) ? value : null; }
        row.Facility = ValueFor("Facility"); row.Manufacture = ValueFor("Manufacture"); row.Product = ValueFor("Product"); row.ProductDescription = ValueFor("Product Description"); row.CustomerUom = ValueFor("Customer UOM");
        var quantity = ValueFor("Customer Qty");
        if (!string.IsNullOrWhiteSpace(quantity) && !decimal.TryParse(quantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedQuantity)) throw new InvalidDataException($"Customer Qty contains an invalid value in sheet {sheet.Name}, row {parsedRow.Number}.");
        if (decimal.TryParse(quantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var validQuantity)) row.CustomerQty = validQuantity;
    }
    private IReadOnlyList<DailyUploadSeriesDto> BuildDailySeries(IReadOnlyList<(string UserName, DateTime UploadedOn)> events, string label) => [new DailyUploadSeriesDto(label, BuildPoints(events, _ => true))];
    private IReadOnlyList<DailyUploadSeriesDto> BuildAdminDailySeries(IReadOnlyList<(string UserName, DateTime UploadedOn)> events)
    {
        var series = new List<DailyUploadSeriesDto> { new("All customers", BuildPoints(events, _ => true)) };
        series.AddRange(events.Select(item => item.UserName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).Select(user => new DailyUploadSeriesDto(user, BuildPoints(events, item => string.Equals(item.UserName, user, StringComparison.OrdinalIgnoreCase)))));
        return series;
    }
    private IReadOnlyList<DailyUploadPointDto> BuildPoints(IReadOnlyList<(string UserName, DateTime UploadedOn)> events, Func<(string UserName, DateTime UploadedOn), bool> predicate)
    {
        var start = Now.Date.AddDays(-29); var counts = events.Where(predicate).GroupBy(item => item.UploadedOn.Date).ToDictionary(group => group.Key, group => (long)group.Count());
        return Enumerable.Range(0, 30).Select(offset => { var date = start.AddDays(offset); return new DailyUploadPointDto(date.ToString("yyyy-MM-dd"), counts.GetValueOrDefault(date)); }).ToList();
    }
}
