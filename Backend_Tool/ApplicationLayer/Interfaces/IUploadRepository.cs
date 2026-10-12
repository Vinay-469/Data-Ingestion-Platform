using CoreModels.Entities;
using CoreModels.DTOs;

namespace ApplicationLayer.Interfaces;

public interface IUploadRepository
{
    Task AddAsync(Upload upload, Token token);
    Task SaveChangesAsync(Token token);
    Task<long> BulkPersistAsync(Upload upload, Token token);
    Task DeleteUploadAsync(long uploadId, Token token);
    Task AddAuditsAsync(IEnumerable<DuplicateAudit> duplicateAudits, IEnumerable<ValidationAudit> validationAudits, Token token);
    Task PersistColumnMetadataAsync(long uploadId, ParsedWorkbook workbook, UploadConfirmationRequest request, Token token);
    Task<int?> GetUserIdAsync(string userName, Token token);
    Task<IReadOnlyList<Upload>> GetHistoryAsync(string userId, Token token);
    Task<IReadOnlyList<Upload>> GetAllHistoryAsync(Token token);
    Task<Upload?> GetOwnedAsync(string userId, long uploadId, Token token);
    Task<IngestionSheet?> GetOwnedSheetAsync(string userId, long sheetId, Token token);
    Task<IReadOnlyList<IngestionRow>> GetOwnedRowsAsync(string userId, long sheetId, int page, int pageSize, string? search, Token token);
    Task<(long Uploads, long Rows, long Sheets, long Duplicates, long Nulls, long PreviousUploads, long PreviousRows)> GetDashboardAsync(string userId, Token token);
    Task<(long Uploads, long Active, long Inactive, long Rows, long Sheets, long Duplicates, long Nulls)> GetAdminDashboardAsync(Token token);
    Task<IReadOnlyList<StandardColumnCoverageDto>> GetAdminStandardColumnCoverageAsync(Token token);
    Task<IReadOnlyList<AdminCustomerVolumeDto>> GetAdminCustomerVolumeAsync(Token token);
    Task<IReadOnlyList<StandardColumnCoverageDto>> GetStandardColumnCoverageAsync(string userId, long uploadId, Token token);
    Task<IReadOnlyList<(string UserName, DateTime UploadedOn)>> GetDailyUploadEventsAsync(string? userName, Token token);
}
