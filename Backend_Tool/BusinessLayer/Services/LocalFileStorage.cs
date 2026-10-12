using ApplicationLayer.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BusinessLayer.Services;

public sealed class LocalFileStorage(IConfiguration configuration) : IFileStorage
{
    public string GetPath(string userId, string originalFileName)
    {
        var safeUser = string.Concat(userId.Where(char.IsLetterOrDigit));
        var safeName = Path.GetFileName(originalFileName);
        var fileName = string.Concat(safeName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        if (string.IsNullOrWhiteSpace(safeUser) || string.IsNullOrWhiteSpace(fileName)) throw new InvalidDataException("The file name is invalid.");
        return Path.Combine(safeUser, fileName);
    }

    public async Task<string> SaveAsync(string userId, string originalFileName, Stream content, Token token)
    {
        var relativePath = GetPath(userId, originalFileName);
        var safeUser = Path.GetDirectoryName(relativePath) ?? string.Empty;
        var fileName = Path.GetFileName(relativePath);
        var root = configuration["FileStorage:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        var directory = Path.Combine(root, safeUser); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        // Validation creates a temporary copy before the duplicate-name check.
        // A previous interrupted validation may have left the same physical
        // file behind, so never fail the API only because that path exists.
        if (File.Exists(path))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            path = Path.Combine(directory, $"{baseName}-{Guid.NewGuid():N}{extension}");
        }

        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(output, token);
        return Path.GetRelativePath(root, path);
    }

    public Task<string> PromoteAsync(string storagePath, string userId, string originalFileName, Token token)
    {
        token.ThrowIfCancellationRequested();
        var root = configuration["FileStorage:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        var canonicalPath = GetPath(userId, originalFileName);
        var source = Path.GetFullPath(Path.Combine(root, storagePath));
        var destination = Path.GetFullPath(Path.Combine(root, canonicalPath));
        var fullRoot = Path.GetFullPath(root);
        if (!source.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The upload storage path is invalid.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) File.Move(source, destination, true);
        return Task.FromResult(canonicalPath);
    }

    public Task<Stream?> OpenReadAsync(string storagePath, Token token)
    {
        var root = configuration["FileStorage:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        var fullPath = Path.GetFullPath(Path.Combine(root, storagePath));
        var fullRoot = Path.GetFullPath(root);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string storagePath, Token token)
    {
        var root = configuration["FileStorage:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        var fullPath = Path.GetFullPath(Path.Combine(root, storagePath));
        var fullRoot = Path.GetFullPath(root);
        if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
