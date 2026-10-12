using BusinessLayer.Services;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Backend_Tool.Tests;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "BackendToolTests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage storage;

    public LocalFileStorageTests()
    {
        storage = new LocalFileStorage(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:RootPath"] = root })
            .Build());
    }

    [Test]
    public async Task SaveAsync_sanitizes_user_and_original_name()
    {
        await using var content = new MemoryStream("hello"u8.ToArray());
        var result = await storage.SaveAsync("user/../unsafe", "../report.CSV", content, Token.None);

        StringAssert.DoesNotContain("..", result);
        StringAssert.EndsWith("report.CSV", result);
        Assert.IsTrue(File.Exists(Path.Combine(root, result)));
        Assert.AreEqual("hello", await File.ReadAllTextAsync(Path.Combine(root, result)));
    }

    [Test]
    public async Task OpenReadAsync_returns_saved_content_and_missing_returns_null()
    {
        await using var content = new MemoryStream("content"u8.ToArray());
        var result = await storage.SaveAsync("user", "data.txt", content, Token.None);

        await using var opened = await storage.OpenReadAsync(result, Token.None);
        using var reader = new StreamReader(opened!);
        Assert.AreEqual("content", await reader.ReadToEndAsync());
        Assert.IsNull(await storage.OpenReadAsync("missing.txt", Token.None));
    }

    [Test]
    public async Task OpenReadAsync_and_DeleteAsync_reject_paths_outside_storage_root()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(outside, "secret");
        try
        {
            var relativeTraversal = Path.Combine("..", Path.GetFileName(outside));
            Assert.IsNull(await storage.OpenReadAsync(relativeTraversal, Token.None));
            await storage.DeleteAsync(relativeTraversal, Token.None);
            Assert.IsTrue(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
