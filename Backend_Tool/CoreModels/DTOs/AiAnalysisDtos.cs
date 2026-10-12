namespace CoreModels.DTOs;

public sealed record AiSchemaAnalysisDto(
    long UploadId,
    string FileName,
    string Model,
    bool MetadataOnly,
    int SheetsAnalyzed,
    int ColumnsAnalyzed,
    string Analysis,
    DateTime GeneratedOn);

public sealed record AiChatRequestDto(string Question);

public sealed record AiChatResponseDto(long UploadId, string Model, bool MetadataOnly, string Answer, DateTime GeneratedOn);

public sealed record AiHelpRequestDto(string Question);

public sealed record AiHelpResponseDto(string Model, string Answer, DateTime GeneratedOn);
