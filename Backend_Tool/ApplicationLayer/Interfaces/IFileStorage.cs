namespace ApplicationLayer.Interfaces;

public interface IFileStorage
{
    Task<string> SaveAsync(string userId, string originalFileName, Stream content, Token token);
    string GetPath(string userId, string originalFileName);
    Task<string> PromoteAsync(string storagePath, string userId, string originalFileName, Token token);
    Task<Stream?> OpenReadAsync(string storagePath, Token token);
    Task DeleteAsync(string storagePath, Token token);
}
