using CoreModels.DTOs;

namespace ApplicationLayer.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, Token token);
    Task<LoginResponse?> RegisterAsync(RegisterRequest request, Token token);
    Task<long> GetUserCountAsync(Token token);
}
