namespace CoreModels.DTOs;

public sealed record NotificationMessage(string Type, string Title, string Message, long? UploadId, string? FileName, string? UserName, DateTime CreatedOn);
