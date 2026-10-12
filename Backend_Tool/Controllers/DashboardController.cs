using ApplicationLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Tool.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(IUploadService uploads) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(Token token) => uploads.GetDashboardAsync(User.Identity?.Name ?? "unknown", token);
}
