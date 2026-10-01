namespace LibraSys.Api.Features.Auth;

public sealed class AuthUser
{
    public long UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public int FailedLoginCount { get; init; }
    public DateTime? LockedUntil { get; init; }
}
