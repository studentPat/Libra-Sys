using Dapper;
using LibraSys.Api.Data;

namespace LibraSys.Api.Features.Librarian;

public sealed class LibrarianReportsRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<MonthlyBorrowingReport>> GetMonthlyBorrowingsAsync(
        int year, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT MONTH(borrow_date) AS Month,
                   COUNT(*) AS BorrowingCount
            FROM borrowings
            WHERE borrow_date >= MAKEDATE(@Year, 1)
              AND borrow_date < MAKEDATE(@Year + 1, 1)
            GROUP BY MONTH(borrow_date)
            ORDER BY Month;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MonthlyBorrowingReport>(
            new CommandDefinition(sql, new { Year = year },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<MemberBalanceReport>> GetMemberBalancesAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(member_id AS SIGNED) AS MemberId,
                   first_name AS FirstName,
                   last_name AS LastName,
                   assessed_fines AS AssessedFines,
                   paid_fines AS PaidFines,
                   balance AS Balance
            FROM v_member_balances
            ORDER BY last_name, first_name, member_id
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MemberBalanceReport>(new CommandDefinition(
            sql, new { PageSize = pageSize, Offset = (page - 1) * pageSize },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CatalogAvailabilityReport>> GetCatalogAvailabilityAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(book_id AS SIGNED) AS BookId,
                   isbn AS Isbn,
                   title AS Title,
                   publisher AS Publisher,
                   publication_year AS PublicationYear,
                   CAST(available_copy_count AS SIGNED) AS AvailableCopyCount
            FROM v_available_catalog
            WHERE @Search IS NULL
               OR title LIKE CONCAT('%', @Search, '%')
               OR isbn LIKE CONCAT('%', @Search, '%')
            ORDER BY title, book_id
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CatalogAvailabilityReport>(new CommandDefinition(
            sql,
            new
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                PageSize = pageSize,
                Offset = (page - 1) * pageSize
            },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(
        string? action, string? entityType, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(t.transaction_id AS SIGNED) AS TransactionId,
                   CAST(t.user_id AS SIGNED) AS UserId,
                   u.username AS Username,
                   t.action AS Action,
                   t.entity_type AS EntityType,
                   CAST(t.entity_id AS SIGNED) AS EntityId,
                   t.event_time AS EventTime,
                   t.success AS Success,
                   CAST(t.details AS CHAR) AS Details
            FROM transaction_logs t
            LEFT JOIN users u ON u.user_id = t.user_id
            WHERE (@Action IS NULL OR t.action = @Action)
              AND (@EntityType IS NULL OR t.entity_type = @EntityType)
            ORDER BY t.event_time DESC, t.transaction_id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AuditLogEntry>(new CommandDefinition(
            sql,
            new
            {
                Action = string.IsNullOrWhiteSpace(action) ? null : action.Trim(),
                EntityType = string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim(),
                PageSize = pageSize,
                Offset = (page - 1) * pageSize
            },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
