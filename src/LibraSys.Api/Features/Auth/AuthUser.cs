namespace LibraSys.Api.Features.Auth;

public sealed record AuthUser(long UserId, string Username, string PasswordHash, string RoleName);
