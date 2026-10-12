using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using ApplicationLayer.Interfaces;
using CoreModels.Configuration;
using CoreModels.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLayer.Services;

public sealed class UploadJobQueue(IServiceScopeFactory scopes, ILogger<UploadJobQueue> logger, IOptions<ApplicationSettings> settings) : BackgroundService, IUploadJobQueue
{
    private readonly ApplicationSettings applicationSettings = settings.Value;
    private DateTime Now => DateTime.UtcNow.AddHours(applicationSettings.Time.IndiaStandardTimeOffsetHours);
    private sealed record Work(Guid JobId, string UserId, UploadConfirmationRequest Request);
    private readonly Channel<Work> queue = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<Guid, UploadJobStatusDto> jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> cancellations = new();
    private readonly ConcurrentDictionary<Guid, string> owners = new();

    public UploadJobStatusDto Enqueue(string userId, UploadConfirmationRequest request)
    {
        var now = Now;
        var job = new UploadJobStatusDto(Guid.NewGuid(), "Processing", "Importing", 0, null, null, now, null, null, null, null, null);
        jobs[job.JobId] = job;
        owners[job.JobId] = userId;
        cancellations[job.JobId] = new CancellationTokenSource();
        queue.Writer.TryWrite(new Work(job.JobId, userId, request));
        return job;
    }

    public UploadJobStatusDto? Get(Guid jobId) => jobs.TryGetValue(jobId, out var job) ? job : null;

    public bool Cancel(string userId, Guid jobId)
    {
        if (!owners.TryGetValue(jobId, out var owner) || owner != userId || !jobs.TryGetValue(jobId, out var job) || job.Status != "Processing") return false;
        if (!cancellations.TryGetValue(jobId, out var cancellation) || cancellation.IsCancellationRequested) return false;
        cancellation.Cancel();
        return true;
    }

    protected override async Task ExecuteAsync(Token token)
    {
        await foreach (var work in queue.Reader.ReadAllAsync(token))
        {
            var started = Now;
            Update(work.JobId, job => job with { Stage = "Importing", Progress = 1, StartedAt = started });
            var timer = Stopwatch.StartNew();
            try
            {
                if (!cancellations.TryGetValue(work.JobId, out var cancellation)) continue;
                using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IUploadService>();
                var progress = new Progress<int>(value => Update(work.JobId, job => job with { Stage = "Importing", Progress = Math.Clamp(value, 1, 99) }));
                var result = await service.ConfirmAsync(work.UserId, work.Request, linkedToken.Token, progress);
                var completed = Now;
                Update(work.JobId, job => job with { Status = "Completed", Stage = "Completed", Progress = 100, Result = result, CompletedAt = completed, ElapsedMilliseconds = (long)timer.Elapsed.TotalMilliseconds, ProcessingMilliseconds = result is null ? null : (long)timer.Elapsed.TotalMilliseconds, StorageMilliseconds = null });
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IUploadService>();
                    await service.CancelValidationAsync(work.UserId, work.Request.ValidationId, Token.None);
                }
                catch (Exception cleanupException) { logger.LogError(cleanupException, "Upload job cleanup failed for {JobId}", work.JobId); }
                CompleteCancelled(work.JobId, timer);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Upload job {JobId} failed", work.JobId);
                try
                {
                    using var scope = scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IUploadService>();
                    await service.CancelValidationAsync(work.UserId, work.Request.ValidationId, Token.None);
                }
                catch (Exception cleanupException) { logger.LogError(cleanupException, "Upload job cleanup failed for {JobId}", work.JobId); }
                CompleteFailure(work.JobId, timer, exception.Message);
            }
            finally
            {
                if (cancellations.TryRemove(work.JobId, out var cancellation)) cancellation.Dispose();
                owners.TryRemove(work.JobId, out _);
            }
        }
    }

    private void CompleteCancelled(Guid jobId, Stopwatch timer) => Update(jobId, job => job with { Status = "Cancelled", Stage = "Cancelled", Progress = 0, ErrorMessage = "Import cancelled. Temporary and partial data were removed.", CompletedAt = Now, ElapsedMilliseconds = (long)timer.Elapsed.TotalMilliseconds, ProcessingMilliseconds = (long)timer.Elapsed.TotalMilliseconds });
    private void CompleteFailure(Guid jobId, Stopwatch timer, string message) => Update(jobId, job => job with { Status = "Failed", Stage = "Failed", Progress = 0, ErrorMessage = message, CompletedAt = Now, ElapsedMilliseconds = (long)timer.Elapsed.TotalMilliseconds, ProcessingMilliseconds = (long)timer.Elapsed.TotalMilliseconds });
    private void Update(Guid jobId, Func<UploadJobStatusDto, UploadJobStatusDto> update) => jobs.AddOrUpdate(jobId, _ => throw new InvalidOperationException("Upload job not found."), (_, current) => update(current));
}
