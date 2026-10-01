using Dapper;
using LibraSys.Api.Data;

namespace LibraSys.Api.Features.Auth;

public sealed class AuthRepository(IDbConnectionFactory connectionFactory) : IAuthRepository
{
    public async Task<AuthUser?> FindActiveUserAsync(
        string username, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(u.user_id AS SIGNED) AS UserId,
                   u.username AS Username,
                   u.password_hash AS PasswordHash,
                   r.role_name AS RoleName,
                   CAST(u.failed_login_count AS SIGNED) AS FailedLoginCount,
                   u.locked_until AS LockedUntil
            FROM users u
            INNER JOIN roles r ON r.role_id = u.role_id
            WHERE u.username = @Username
              AND u.status = 'active';
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AuthUser>(
            new CommandDefinition(sql, new { Username = username },
                cancellationToken: cancellationToken));
    }

    public async Task RecordFailedLoginAsync(
        long userId, int failedLoginCount, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE users
            SET failed_login_count = @FailedLoginCount,
                locked_until = CASE
                    WHEN @FailedLoginCount >= 5
                    THEN DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 15 MINUTE)
                    ELSE locked_until
                END
            WHERE user_id = @UserId
              AND status = 'active';
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { UserId = userId, FailedLoginCount = failedLoginCount },
            cancellationToken: cancellationToken));
    }

    public async Task ResetFailedLoginsAsync(
        long userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE users
            SET failed_login_count = 0,
                locked_until = NULL
            WHERE user_id = @UserId;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { UserId = userId }, cancellationToken: cancellationToken));
    }
}
