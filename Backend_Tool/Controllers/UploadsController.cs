using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Tool.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public sealed class UploadsController(IUploadService uploads, IUploadJobQueue uploadJobs, ILogger<UploadsController> logger) : ControllerBase
{
    private string UserId => User.Identity?.Name ?? User.FindFirst("sub")?.Value ?? "unknown";
    private string DataAccessUserId => User.IsInRole("Admin") ? "*" : UserId;

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, Token token)
    {
        logger.LogInformation("Upload request received for {FileName} by {UserId}", file?.FileName, UserId);
        return Ok(await uploads.UploadAsync(UserId, file, token));
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(IFormFile file, Token token)
    {
        logger.LogInformation("Upload validation request received for {FileName} by {UserId}", file?.FileName, UserId);
        return Ok(await uploads.ValidateAsync(UserId, file, token));
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] UploadConfirmationRequest request, Token token)
    {
        logger.LogInformation("Upload confirmation requested by {UserId}", UserId);
        var result = await uploads.ConfirmAsync(UserId, request, token);
        return result is null ? NotFound(new { message = "The validation session was not found or has expired." }) : Ok(result);
    }

    [HttpDelete("validate/{validationId:guid}")]
    public async Task<IActionResult> CancelValidation(Guid validationId, Token token) => await uploads.CancelValidationAsync(UserId, validationId, token) ? NoContent() : NotFound();

    [HttpPost("confirm-async")]
    public IActionResult ConfirmAsync([FromBody] UploadConfirmationRequest request)
    {
        var job = uploadJobs.Enqueue(UserId, request);
        return AcceptedAtAction(nameof(GetJob), new { jobId = job.JobId }, job);
    }

    [HttpGet("jobs/{jobId:guid}")]
    public IActionResult GetJob(Guid jobId) => uploadJobs.Get(jobId) is { } job ? Ok(job) : NotFound(new { message = "The upload job was not found or has expired." });

    [HttpDelete("jobs/{jobId:guid}")]
    public IActionResult CancelJob(Guid jobId) => uploadJobs.Cancel(UserId, jobId) ? Accepted(uploadJobs.Get(jobId)) : NotFound(new { message = "The upload job was not found, already completed, or does not belong to you." });

    [HttpGet]
    public Task<IReadOnlyList<UploadSummaryDto>> History(Token token) => User.IsInRole("Admin") ? uploads.GetAllHistoryAsync(token) : uploads.GetHistoryAsync(UserId, token);

    [HttpGet("{uploadId:long}/sheets")]
    public Task<IReadOnlyList<SheetDto>> Sheets(long uploadId, Token token) => uploads.GetSheetsAsync(DataAccessUserId, uploadId, token);

    [HttpGet("{uploadId:long}/standard-coverage")]
    public Task<IReadOnlyList<StandardColumnCoverageDto>> StandardCoverage(long uploadId, Token token) => uploads.GetStandardColumnCoverageAsync(DataAccessUserId, uploadId, token);

    [HttpGet("sheets/{sheetId:long}/columns")]
    public Task<IReadOnlyList<ColumnDto>> Columns(long sheetId, Token token) => uploads.GetColumnsAsync(DataAccessUserId, sheetId, token);

    [HttpGet("sheets/{sheetId:long}/rows")]
    public Task<IReadOnlyList<RowDto>> Rows(long sheetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, Token token = default) => uploads.GetRowsAsync(DataAccessUserId, sheetId, page, pageSize, search, token);

    [HttpGet("{uploadId:long}/download")]
    public async Task<IActionResult> Download(long uploadId, Token token)
    {
        var stream = await uploads.DownloadAsync(DataAccessUserId, uploadId, token);
        return stream is null ? NotFound() : File(stream, "application/octet-stream");
    }

    [HttpPut("{uploadId:long}/status")]
    public async Task<IActionResult> SetStatus(long uploadId, [FromBody] UploadStatusRequest request, Token token)
    {
        if (User.IsInRole("Admin")) return Forbid();
        return await uploads.SetStatusAsync(UserId, uploadId, request.IsActive, token) ? Ok(new { status = "Deleted", deleted = true }) : NotFound();
    }
}

public sealed record UploadStatusRequest(bool IsActive);
