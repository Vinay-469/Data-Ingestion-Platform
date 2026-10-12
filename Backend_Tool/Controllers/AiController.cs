using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Tool.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AiController(IAiAnalysisService analysis, ILogger<AiController> logger) : ControllerBase
{
    private string UserId => User.Identity?.Name ?? User.FindFirst("sub")?.Value ?? "unknown";

    [HttpGet("schema-analysis/{uploadId:long}")]
    public async Task<IActionResult> AnalyzeSchema(long uploadId, Token token)
    {
        logger.LogInformation("AI schema analysis requested for upload {UploadId} by {UserId}", uploadId, UserId);
        var result = await analysis.AnalyzeSchemaAsync(User.IsInRole("Admin") ? "*" : UserId, uploadId, token);
        return result is null ? NotFound(new { message = "The upload was not found or is inactive." }) : Ok(result);
    }

    [HttpPost("chat/{uploadId:long}")]
    public async Task<IActionResult> Ask(long uploadId, [FromBody] AiChatRequestDto request, Token token)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Question)) return BadRequest(new { message = "Please enter a question." });
        logger.LogInformation("AI workbook question requested for upload {UploadId} by {UserId}", uploadId, UserId);
        var result = await analysis.AskAsync(User.IsInRole("Admin") ? "*" : UserId, uploadId, request.Question.Trim(), token);
        return result is null ? NotFound(new { message = "The upload was not found or is inactive." }) : Ok(result);
    }

    [HttpPost("help")]
    public async Task<IActionResult> Help([FromBody] AiHelpRequestDto request, Token token)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Question)) return BadRequest(new { message = "Please enter a help question." });
        logger.LogInformation("AI application help requested by {UserId}", UserId);
        return Ok(await analysis.AskHelpAsync(request.Question.Trim(), token));
    }

    [HttpPost("column-mapping/{validationId:guid}")]
    public async Task<IActionResult> MapColumns(Guid validationId, Token token)
    {
        logger.LogInformation("AI column mapping requested for validation {ValidationId} by {UserId}", validationId, UserId);
        var result = await analysis.MapPendingColumnsAsync(UserId, validationId, token);
        return result is null ? NotFound(new { message = "The validation session was not found, expired, or does not belong to you." }) : Ok(result);
    }
}
