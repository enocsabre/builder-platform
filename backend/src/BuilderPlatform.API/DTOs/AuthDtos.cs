namespace BuilderPlatform.API.DTOs;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? Name);
public record LoginResponse(string Token, string Email, DateTime ExpiresAt);
public record MeResponse(string Email, Guid UserId, string? Name);
