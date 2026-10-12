namespace CoreModels.DTOs;

public sealed record LoginRequest(string UserName, string Password);
public sealed record RegisterRequest(string UserName, string Password);
public sealed record LoginResponse(string AccessToken, string UserName, string Role, DateTime ExpiresAt);
