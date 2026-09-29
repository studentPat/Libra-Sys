using Dapper;
using LibraSys.Api.Data;

namespace LibraSys.Api.Features.Members;

public sealed class MemberRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<MemberProfile?> GetProfileAsync(
        long userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(m.member_id AS SIGNED) AS MemberId,
                   u.username AS Username,
                   m.first_name AS FirstName,
                   m.last_name AS LastName,
                   m.email AS Email,
                   m.contact_info AS ContactInfo,
                   m.member_status AS Status
            FROM members m
            INNER JOIN users u ON u.user_id = m.user_id
            WHERE m.user_id = @UserId;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<MemberProfile>(
            new CommandDefinition(sql, new { UserId = userId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MemberBorrowing>> GetBorrowingsAsync(
        long userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(br.borrowing_id AS SIGNED) AS BorrowingId,
                   b.title AS Title,
                   bc.accession_number AS AccessionNumber,
                   br.borrow_date AS BorrowDate,
                   br.due_date AS DueDate,
                   br.return_date AS ReturnDate,
                   br.status AS Status,
                   br.return_condition AS ReturnCondition
            FROM borrowings br
            INNER JOIN members m ON m.member_id = br.member_id
            INNER JOIN book_copies bc ON bc.copy_id = br.copy_id
            INNER JOIN books b ON b.book_id = bc.book_id
            WHERE m.user_id = @UserId
            ORDER BY br.borrow_date DESC;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MemberBorrowing>(
            new CommandDefinition(sql, new { UserId = userId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<bool> UpdateProfileAsync(
        long userId, UpdateMemberProfileRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE members
            SET first_name = @FirstName,
                last_name = @LastName,
                email = @Email,
                contact_info = @ContactInfo
            WHERE user_id = @UserId
              AND member_status <> 'closed';
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                UserId = userId,
                request.FirstName,
                request.LastName,
                request.Email,
                request.ContactInfo
            },
            cancellationToken: cancellationToken));
        return affected > 0;
    }
}
