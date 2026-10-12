namespace CoreModels.Configuration;

public sealed class ApplicationSettings
{
    public TimeSettings Time { get; set; } = new();
    public UploadSettings Upload { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
}

public sealed class TimeSettings
{
    public double IndiaStandardTimeOffsetHours { get; set; } = 5.5;
}

public sealed class UploadSettings
{
    public long MaxFileSizeBytes { get; set; } = 100_000_000;
    public int ValidationExpiryMinutes { get; set; } = 30;
}

public sealed class NotificationSettings
{
    public string UploadCompletedTitle { get; set; } = "Upload completed";
    public string UploadCompletedMessage { get; set; } = "The file has been uploaded successfully.";
    public string NewCustomerUploadTitle { get; set; } = "New customer upload";
    public string NewCustomerUploadMessage { get; set; } = "A new customer file has been uploaded.";
    public string FileStateTitle { get; set; } = "File Status";
    public string FileStateMessage { get; set; } = "The file status has been updated.";
    public string CustomerFileStateTitle { get; set; } = "Customer File Status";
    public string CustomerFileStateMessage { get; set; } = "The customer file status has been updated.";
}
