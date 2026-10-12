using CoreModels.DTOs;

namespace ApplicationLayer.Interfaces;

public interface IAiAnalysisService
{
    Task<AiSchemaAnalysisDto?> AnalyzeSchemaAsync(string userId, long uploadId, Token token);
    Task<AiChatResponseDto?> AskAsync(string userId, long uploadId, string question, Token token);
    Task<AiHelpResponseDto> AskHelpAsync(string question, Token token);
    Task<ColumnMappingResultDto?> MapPendingColumnsAsync(string userId, Guid validationId, Token token);
}
