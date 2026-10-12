using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Tool.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(IUploadService uploads, IAuthService auth) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<AdminDashboardDto> Dashboard(Token token) => await uploads.GetAdminDashboardAsync(await auth.GetUserCountAsync(token), token);

    [HttpGet("uploads")]
    public Task<IReadOnlyList<UploadSummaryDto>> Uploads(Token token) => uploads.GetAllHistoryAsync(token);
}
