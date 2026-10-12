using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApplicationLayer.Interfaces;
using CoreModels.DTOs;
using CoreModels.Entities;
using DataAccessLayer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BusinessLayer.Services;

public sealed class AuthService(IngestionDbContext database, IConfiguration configuration) : IAuthService
{
    private readonly int jwtExpirationMinutes = configuration.GetValue<int?>("Jwt:ExpirationMinutes") ?? 30;
    private DateTime Now => DateTime.UtcNow.AddHours(configuration.GetValue<double?>("Application:Time:IndiaStandardTimeOffsetHours") ?? 5.5);
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, Token token)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password)) return null;

        var userName = request.UserName!.Trim();
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(item => item.UserName == userName && item.IsActive, token);
        if (user is null || !PasswordHashing.Verify(request.Password!, user.PasswordHash)) return null;
        return CreateToken(user);
    }

    public async Task<LoginResponse?> RegisterAsync(RegisterRequest request, Token token)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password)) return null;

        var userName = request.UserName!.Trim();
        if (userName.Length < 3 || request.Password.Length < 8) return null;
        if (await database.Users.AnyAsync(item => item.UserName == userName, token)) return null;
        var user = new AppUser { UserName = userName, PasswordHash = PasswordHashing.Hash(request.Password), Role = "Customer", CreatedOn = Now, IsActive = true };
        database.Users.Add(user);
        await database.SaveChangesAsync(token);
        return CreateToken(user);
    }

    public Task<long> GetUserCountAsync(Token token) => database.Users.LongCountAsync(item => item.IsActive, token);

    private LoginResponse CreateToken(AppUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(jwtExpirationMinutes);
        var claims = new[] { new Claim(ClaimTypes.Name, user.UserName), new Claim(ClaimTypes.Role, user.Role), new Claim("sub", user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey)) jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
        if (string.IsNullOrWhiteSpace(jwtKey)) throw new InvalidOperationException("JWT_KEY or Jwt:Key must be configured outside source control.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var token = new JwtSecurityToken(claims: claims, expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), user.UserName, user.Role, expires);
    }
}
