using ApplicationLayer.Interfaces;
using CoreModels.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BusinessLayer.Services;

public sealed class FileValidationService(IOptions<ApplicationSettings> settings) : IFileValidationService
{
    private readonly UploadSettings uploadSettings = settings.Value.Upload;
    public FileValidationService() : this(Options.Create(new ApplicationSettings())) { }
    private static readonly HashSet<string> Extensions = [".xlsx", ".xls", ".csv", ".tsv", ".txt", ".json", ".xml", ".pdf", ".docx", ".zip"];
    public void Validate(IFormFile file)
    {
        if (file is null || file.Length == 0) throw new InvalidDataException("The file is empty.");
        if (file.Length > uploadSettings.MaxFileSizeBytes)
        {
            var maxSizeMb = uploadSettings.MaxFileSizeBytes / 1_000_000d;
            throw new InvalidDataException($"The maximum file size is {maxSizeMb:0.##} MB.");
        }
        if (!Extensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) throw new InvalidDataException("Supported formats are XLSX, XLS, CSV, TSV, TXT, JSON, XML, PDF, DOCX, and ZIP. Legacy DOC files are not supported.");
    }
}
