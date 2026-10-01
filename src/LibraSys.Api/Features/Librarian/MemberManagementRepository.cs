using Dapper;
using LibraSys.Api.Data;
using MySqlConnector;

namespace LibraSys.Api.Features.Librarian;

public sealed class MemberManagementRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<LibrarianMemberFine>> GetFinesAsync(
        long memberId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(f.fine_id AS SIGNED) AS FineId,
                   CAST(f.borrowing_id AS SIGNED) AS BorrowingId,
                   f.amount AS Amount,
                   f.reason AS Reason,
                   f.status AS Status,
                   f.created_at AS CreatedAt,
                   COALESCE(SUM(p.amount_paid), 0.00) AS PaidAmount,
                   GREATEST(f.amount - COALESCE(SUM(p.amount_paid), 0.00), 0.00)
                       AS RemainingAmount
            FROM fines f
            INNER JOIN borrowings br ON br.borrowing_id = f.borrowing_id
            LEFT JOIN payments p ON p.fine_id = f.fine_id
            WHERE br.member_id = @MemberId
            GROUP BY f.fine_id, f.borrowing_id, f.amount, f.reason,
                     f.status, f.created_at
            ORDER BY f.created_at DESC, f.fine_id DESC;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<LibrarianMemberFine>(
            new CommandDefinition(sql, new { MemberId = memberId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<LibrarianMemberPayment>> GetPaymentsAsync(
        long memberId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(p.payment_id AS SIGNED) AS PaymentId,
                   CAST(p.fine_id AS SIGNED) AS FineId,
                   p.amount_paid AS AmountPaid,
                   p.payment_date AS PaymentDate,
                   p.payment_method AS PaymentMethod,
                   p.receipt_reference AS ReceiptReference
            FROM payments p
            INNER JOIN fines f ON f.fine_id = p.fine_id
            INNER JOIN borrowings br ON br.borrowing_id = f.borrowing_id
            WHERE br.member_id = @MemberId
            ORDER BY p.payment_date DESC, p.payment_id DESC;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<LibrarianMemberPayment>(
            new CommandDefinition(sql, new { MemberId = memberId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<LibrarianMemberBorrowing>> GetBorrowingsAsync(
        long memberId, string? status, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(br.borrowing_id AS SIGNED) AS BorrowingId,
                   CAST(br.member_id AS SIGNED) AS MemberId,
                   b.title AS Title,
                   bc.accession_number AS AccessionNumber,
                   br.borrow_date AS BorrowDate,
                   br.due_date AS DueDate,
                   br.return_date AS ReturnDate,
                   br.status AS Status,
                   br.return_condition AS ReturnCondition
            FROM borrowings br
            INNER JOIN book_copies bc ON bc.copy_id = br.copy_id
            INNER JOIN books b ON b.book_id = bc.book_id
            WHERE br.member_id = @MemberId
              AND (@Status IS NULL OR br.status = @Status)
            ORDER BY br.borrow_date DESC, br.borrowing_id DESC;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<LibrarianMemberBorrowing>(
            new CommandDefinition(sql,
                new { MemberId = memberId, Status = status },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

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
