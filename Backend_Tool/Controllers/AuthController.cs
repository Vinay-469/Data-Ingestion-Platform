using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend_Tool.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, Token token)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required." });

        return await auth.LoginAsync(request, token) is { } response
            ? Ok(response)
            : Unauthorized(new { message = "Invalid username or password." });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, Token token) => await auth.RegisterAsync(request, token) is { } response ? Ok(response) : Conflict(new { message = "Registration failed. Use a unique username and a password of at least 8 characters." });
}
