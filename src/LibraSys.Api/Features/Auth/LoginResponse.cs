namespace LibraSys.Api.Features.Auth;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string Username,
    string Role);
