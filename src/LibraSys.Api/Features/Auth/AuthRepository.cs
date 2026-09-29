using Dapper;
using LibraSys.Api.Data;

namespace LibraSys.Api.Features.Auth;

public sealed class AuthRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<AuthUser?> FindActiveUserAsync(
        string username, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(u.user_id AS SIGNED) AS UserId,
                   u.username AS Username,
                   u.password_hash AS PasswordHash,
                   r.role_name AS RoleName
            FROM users u
            INNER JOIN roles r ON r.role_id = u.role_id
            WHERE u.username = @Username
              AND u.status = 'active'
              AND (u.locked_until IS NULL OR u.locked_until <= CURRENT_TIMESTAMP);
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AuthUser>(
            new CommandDefinition(sql, new { Username = username },
                cancellationToken: cancellationToken));
    }
}
