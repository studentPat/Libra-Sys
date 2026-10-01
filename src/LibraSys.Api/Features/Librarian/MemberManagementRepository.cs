using Dapper;
using LibraSys.Api.Data;
using MySqlConnector;

namespace LibraSys.Api.Features.Librarian;

public sealed class MemberManagementRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<LibrarianMember>> ListAsync(
        string? search, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(m.member_id AS SIGNED) AS MemberId,
                   CAST(m.user_id AS SIGNED) AS UserId,
                   u.username AS Username,
                   m.first_name AS FirstName,
                   m.last_name AS LastName,
                   m.email AS Email,
                   m.contact_info AS ContactInfo,
                   m.member_status AS Status
            FROM members m
            INNER JOIN users u ON u.user_id = m.user_id
            WHERE (@Search IS NULL OR m.first_name LIKE CONCAT('%', @Search, '%')
                   OR m.last_name LIKE CONCAT('%', @Search, '%')
                   OR m.email LIKE CONCAT('%', @Search, '%')
                   OR u.username LIKE CONCAT('%', @Search, '%'))
              AND (@Status IS NULL OR m.member_status = @Status)
            ORDER BY m.last_name, m.first_name, m.member_id
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<LibrarianMember>(new CommandDefinition(
            sql,
            new
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                Status = string.IsNullOrWhiteSpace(status) ? null : status,
                PageSize = pageSize,
                Offset = (page - 1) * pageSize
            },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<MemberManagementResult?> UpdateStatusAsync(
        long actorUserId, long memberId, string status, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string updateSql = """
                UPDATE members
                SET member_status = @Status
                WHERE member_id = @MemberId;
                """;
            var affected = await connection.ExecuteAsync(new CommandDefinition(updateSql,
                new { MemberId = memberId, Status = status },
                transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'update_member_status', 'member', @MemberId,
                        JSON_OBJECT('status', @Status));
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new { UserId = actorUserId, MemberId = memberId, Status = status },
                transaction: transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new MemberManagementResult(memberId, status);
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<MemberManagementResult?> UpdateAsync(
        long actorUserId, long memberId, UpdateMemberRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string updateSql = """
                UPDATE members
                SET first_name = @FirstName,
                    last_name = @LastName,
                    email = @Email,
                    contact_info = @ContactInfo
                WHERE member_id = @MemberId;
                """;
            var affected = await connection.ExecuteAsync(new CommandDefinition(updateSql,
                new
                {
                    MemberId = memberId,
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.ContactInfo
                },
                transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'update_member', 'member', @MemberId,
                        JSON_OBJECT('email', @Email));
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new { UserId = actorUserId, MemberId = memberId, request.Email },
                transaction: transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new MemberManagementResult(memberId, "updated");
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
