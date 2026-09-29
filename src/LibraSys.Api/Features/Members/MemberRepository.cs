using Dapper;
using LibraSys.Api.Data;
using MySqlConnector;

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

    public async Task<BorrowingActionResult?> BorrowAsync(
        long userId, long copyId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            const string memberSql = """
                SELECT CAST(member_id AS SIGNED)
                FROM members
                WHERE user_id = @UserId
                  AND member_status = 'active'
                FOR UPDATE;
                """;
            var memberId = await connection.QuerySingleOrDefaultAsync<long?>(
                new CommandDefinition(memberSql, new { UserId = userId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (memberId is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string copySql = """
                SELECT status
                FROM book_copies
                WHERE copy_id = @CopyId
                FOR UPDATE;
                """;
            var copyStatus = await connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(copySql, new { CopyId = copyId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (!string.Equals(copyStatus, "available", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string insertSql = """
                INSERT INTO borrowings (copy_id, member_id, due_date)
                VALUES (@CopyId, @MemberId,
                        DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 14 DAY));
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertSql,
                new { CopyId = copyId, MemberId = memberId.Value },
                transaction: transaction, cancellationToken: cancellationToken));
            var borrowingId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    "SELECT CAST(LAST_INSERT_ID() AS SIGNED);",
                    transaction: transaction, cancellationToken: cancellationToken));
            var dueDate = await connection.ExecuteScalarAsync<DateTime>(
                new CommandDefinition(
                    """
                    SELECT due_date
                    FROM borrowings
                    WHERE borrowing_id = @BorrowingId;
                    """,
                    new { BorrowingId = borrowingId },
                    transaction: transaction, cancellationToken: cancellationToken));

            const string copyUpdateSql = """
                UPDATE book_copies
                SET status = 'borrowed'
                WHERE copy_id = @CopyId;
                """;
            await connection.ExecuteAsync(new CommandDefinition(copyUpdateSql,
                new { CopyId = copyId }, transaction: transaction,
                cancellationToken: cancellationToken));

            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'borrow', 'copy', @CopyId,
                        JSON_OBJECT('borrowing_id', @BorrowingId));
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new { UserId = userId, CopyId = copyId, BorrowingId = borrowingId },
                transaction: transaction, cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new BorrowingActionResult(
                borrowingId,
                dueDate,
                "active");
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<BorrowingActionResult?> ReturnAsync(
        long userId, long borrowingId, string returnCondition,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            const string borrowingSql = """
                SELECT CAST(br.copy_id AS SIGNED) AS CopyId,
                       br.status AS Status
                FROM borrowings br
                INNER JOIN members m ON m.member_id = br.member_id
                WHERE br.borrowing_id = @BorrowingId
                  AND m.user_id = @UserId
                FOR UPDATE;
                """;
            var borrowing = await connection.QuerySingleOrDefaultAsync<ReturnBorrowing>(
                new CommandDefinition(borrowingSql,
                    new { BorrowingId = borrowingId, UserId = userId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (borrowing is null ||
                !string.Equals(borrowing.Status, "active", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string borrowingUpdateSql = """
                UPDATE borrowings
                SET status = 'returned',
                    return_date = CURRENT_TIMESTAMP,
                    return_condition = @ReturnCondition
                WHERE borrowing_id = @BorrowingId;
                """;
            await connection.ExecuteAsync(new CommandDefinition(borrowingUpdateSql,
                new { BorrowingId = borrowingId, ReturnCondition = returnCondition },
                transaction: transaction, cancellationToken: cancellationToken));

            const string copyUpdateSql = """
                UPDATE book_copies
                SET status = 'available',
                    item_condition = @ReturnCondition
                WHERE copy_id = @CopyId;
                """;
            await connection.ExecuteAsync(new CommandDefinition(copyUpdateSql,
                new { CopyId = borrowing.CopyId, ReturnCondition = returnCondition },
                transaction: transaction, cancellationToken: cancellationToken));

            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'return', 'borrowing', @BorrowingId,
                        JSON_OBJECT('return_condition', @ReturnCondition));
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new
                {
                    UserId = userId,
                    BorrowingId = borrowingId,
                    ReturnCondition = returnCondition
                },
                transaction: transaction, cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new BorrowingActionResult(borrowingId, DateTime.UtcNow, "returned");
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<MemberReservation>> GetReservationsAsync(
        long userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CAST(r.reservation_id AS SIGNED) AS ReservationId,
                   CAST(r.book_id AS SIGNED) AS BookId,
                   b.title AS Title,
                   r.reserved_at AS ReservedAt,
                   r.expires_at AS ExpiresAt,
                   r.status AS Status
            FROM reservations r
            INNER JOIN members m ON m.member_id = r.member_id
            INNER JOIN books b ON b.book_id = r.book_id
            WHERE m.user_id = @UserId
            ORDER BY r.reserved_at DESC;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MemberReservation>(
            new CommandDefinition(sql, new { UserId = userId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<ReservationActionResult?> CreateReservationAsync(
        long userId, long bookId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            const string memberSql = """
                SELECT CAST(member_id AS SIGNED)
                FROM members
                WHERE user_id = @UserId
                  AND member_status = 'active'
                FOR UPDATE;
                """;
            var memberId = await connection.QuerySingleOrDefaultAsync<long?>(
                new CommandDefinition(memberSql, new { UserId = userId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (memberId is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string bookSql = """
                SELECT book_id
                FROM books
                WHERE book_id = @BookId
                FOR UPDATE;
                """;
            var existingBook = await connection.QuerySingleOrDefaultAsync<long?>(
                new CommandDefinition(bookSql, new { BookId = bookId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (existingBook is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string duplicateSql = """
                SELECT reservation_id
                FROM reservations
                WHERE member_id = @MemberId
                  AND book_id = @BookId
                  AND status IN ('queued', 'ready')
                FOR UPDATE;
                """;
            var duplicate = await connection.QuerySingleOrDefaultAsync<long?>(
                new CommandDefinition(duplicateSql,
                    new { MemberId = memberId.Value, BookId = bookId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (duplicate is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string insertSql = """
                INSERT INTO reservations (member_id, book_id)
                VALUES (@MemberId, @BookId);
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertSql,
                new { MemberId = memberId.Value, BookId = bookId },
                transaction: transaction, cancellationToken: cancellationToken));
            var reservationId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    "SELECT CAST(LAST_INSERT_ID() AS SIGNED);",
                    transaction: transaction, cancellationToken: cancellationToken));

            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'reserve', 'book', @BookId,
                        JSON_OBJECT('reservation_id', @ReservationId));
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new { UserId = userId, BookId = bookId, ReservationId = reservationId },
                transaction: transaction, cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new ReservationActionResult(reservationId, "queued");
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> CancelReservationAsync(
        long userId, long reservationId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            const string updateSql = """
                UPDATE reservations r
                INNER JOIN members m ON m.member_id = r.member_id
                SET r.status = 'cancelled',
                    r.cancelled_at = CURRENT_TIMESTAMP
                WHERE r.reservation_id = @ReservationId
                  AND m.user_id = @UserId
                  AND r.status IN ('queued', 'ready');
                """;
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                updateSql, new { UserId = userId, ReservationId = reservationId },
                transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'cancel_reservation', 'reservation', @ReservationId,
                        JSON_OBJECT());
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new { UserId = userId, ReservationId = reservationId },
                transaction: transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<MemberFine>> GetFinesAsync(
        long userId, CancellationToken cancellationToken)
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
            INNER JOIN members m ON m.member_id = br.member_id
            LEFT JOIN payments p ON p.fine_id = f.fine_id
            WHERE m.user_id = @UserId
            GROUP BY f.fine_id, f.borrowing_id, f.amount, f.reason,
                     f.status, f.created_at
            ORDER BY f.created_at DESC;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MemberFine>(
            new CommandDefinition(sql, new { UserId = userId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<MemberPayment>> GetPaymentsAsync(
        long userId, CancellationToken cancellationToken)
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
            INNER JOIN members m ON m.member_id = br.member_id
            WHERE m.user_id = @UserId
            ORDER BY p.payment_date DESC;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<MemberPayment>(
            new CommandDefinition(sql, new { UserId = userId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private sealed record ReturnBorrowing(long CopyId, string Status);
}
