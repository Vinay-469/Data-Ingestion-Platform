using ApplicationLayer.Interfaces;
using CoreModels.Configuration;
using Microsoft.Extensions.Options;

namespace BusinessLayer.Services;

public sealed record PendingUpload(
    Guid ValidationId,
    string UserId,
    string OriginalFileName,
    string FileExtension,
    string StoragePath,
    ParsedWorkbook Workbook,
    DateTime ExpiresAt,
    UploadValidationMetrics Metrics);

public sealed record UploadValidationMetrics(
    int TotalSheets,
    long TotalRows,
    int TotalColumns,
    long DuplicateRows,
    long NullRows,
    bool DuplicateFile,
    int DuplicateSheetNames,
    int DuplicateColumns,
    int EmptySheets,
    bool EmptyWorkbook,
    long InvalidDataTypeCells,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> AdditionalColumns);

public interface IPendingUploadStore
{
    void Add(PendingUpload upload);
    bool TryGet(Guid validationId, out PendingUpload? upload);
    bool Remove(Guid validationId, out PendingUpload? upload);
}

public sealed class PendingUploadStore(IOptions<ApplicationSettings> settings) : IPendingUploadStore
{
    private readonly ApplicationSettings applicationSettings = settings.Value;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, PendingUpload> uploads = new();
    private DateTime Now => DateTime.UtcNow.AddHours(applicationSettings.Time.IndiaStandardTimeOffsetHours);

    public void Add(PendingUpload upload) => uploads[upload.ValidationId] = upload;

    public bool TryGet(Guid validationId, out PendingUpload? upload)
    {
        if (uploads.TryGetValue(validationId, out var value) && value.ExpiresAt > Now)
        {
            upload = value;
            return true;
        }

        uploads.TryRemove(validationId, out _);
        upload = null;
        return false;
    }

    public bool Remove(Guid validationId, out PendingUpload? upload) => uploads.TryRemove(validationId, out upload);
}
