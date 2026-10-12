using CoreModels.DTOs;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Interfaces;

public interface IUploadService
{
    Task<ValidationSummaryDto> UploadAsync(string userId, IFormFile file, Token token);
    Task<UploadValidationResultDto> ValidateAsync(string userId, IFormFile file, Token token);
    Task<ValidationSummaryDto?> ConfirmAsync(string userId, UploadConfirmationRequest request, Token token, IProgress<int>? progress = null);
    Task<bool> CancelValidationAsync(string userId, Guid validationId, Token token);
    Task<IReadOnlyList<UploadSummaryDto>> GetHistoryAsync(string userId, Token token);
    Task<IReadOnlyList<UploadSummaryDto>> GetAllHistoryAsync(Token token);
    Task<IReadOnlyList<SheetDto>> GetSheetsAsync(string userId, long uploadId, Token token);
    Task<IReadOnlyList<ColumnDto>> GetColumnsAsync(string userId, long sheetId, Token token);
    Task<IReadOnlyList<RowDto>> GetRowsAsync(string userId, long sheetId, int page, int pageSize, string? search, Token token);
    Task<Stream?> DownloadAsync(string userId, long uploadId, Token token);
    Task<DashboardDto> GetDashboardAsync(string userId, Token token);
    Task<AdminDashboardDto> GetAdminDashboardAsync(long totalUsers, Token token);
    Task<IReadOnlyList<StandardColumnCoverageDto>> GetStandardColumnCoverageAsync(string userId, long uploadId, Token token);
    Task<bool> SetStatusAsync(string userId, long uploadId, bool isActive, Token token);
}
