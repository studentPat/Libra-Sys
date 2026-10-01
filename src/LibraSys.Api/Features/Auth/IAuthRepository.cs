namespace LibraSys.Api.Features.Auth;

public interface IAuthRepository
{
    Task<AuthUser?> FindActiveUserAsync(string username, CancellationToken cancellationToken);
    Task RecordFailedLoginAsync(long userId, int failedLoginCount, CancellationToken cancellationToken);
    Task ResetFailedLoginsAsync(long userId, CancellationToken cancellationToken);
}
