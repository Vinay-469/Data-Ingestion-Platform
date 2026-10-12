using ApplicationLayer.Interfaces;
using BusinessLayer.Services;
using CoreModels;
using CoreModels.Configuration;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Backend_Tool.Tests;

public sealed class PendingUploadStoreTests
{
    [Test]
    public void Add_and_get_returns_pending_upload()
    {
        var store = CreateStore();
        var upload = CreateUpload(DateTime.UtcNow.AddHours(5.5).AddMinutes(5));
        store.Add(upload);

        Assert.IsTrue(store.TryGet(upload.ValidationId, out var actual));
        Assert.AreSame(upload, actual);
    }

    [Test]
    public void Expired_upload_is_not_returned_and_is_removed()
    {
        var store = CreateStore();
        var upload = CreateUpload(DateTime.UtcNow.AddHours(5.5).AddSeconds(-1));
        store.Add(upload);

        Assert.IsFalse(store.TryGet(upload.ValidationId, out var actual));
        Assert.IsNull(actual);
        Assert.IsFalse(store.Remove(upload.ValidationId, out _));
    }

    [Test]
    public void Remove_returns_and_deletes_upload()
    {
        var store = CreateStore();
        var upload = CreateUpload(DateTime.UtcNow.AddHours(5.5).AddMinutes(5));
        store.Add(upload);

        Assert.IsTrue(store.Remove(upload.ValidationId, out var removed));
        Assert.AreSame(upload, removed);
        Assert.IsFalse(store.TryGet(upload.ValidationId, out _));
    }

    private static PendingUpload CreateUpload(DateTime expiresAt) => new(
        Guid.NewGuid(), "user", "data.csv", ".csv", "data.csv",
        new ParsedWorkbook([]), expiresAt,
        new UploadValidationMetrics(0, 0, 0, 0, 0, false, 0, 0, 0, false, 0, [], []));

    private static PendingUploadStore CreateStore() => new(Options.Create(new ApplicationSettings()));
}
