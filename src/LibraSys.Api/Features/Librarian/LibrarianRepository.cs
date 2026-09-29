using Dapper;
using LibraSys.Api.Data;
using MySqlConnector;

namespace LibraSys.Api.Features.Librarian;

public sealed class LibrarianRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<LibrarianActionResult?> CreateFineAsync(
        long actorUserId, CreateFineRequest request,
        CancellationToken cancellationToken)
    {
        const string borrowingSql = """
            SELECT borrowing_id
            FROM borrowings
            WHERE borrowing_id = @BorrowingId
            FOR UPDATE;
            """;
        const string insertSql = """
            INSERT INTO fines (borrowing_id, amount, reason)
            VALUES (@BorrowingId, @Amount, @Reason);
            """;
        const string logSql = """
            INSERT INTO transaction_logs
                (user_id, action, entity_type, entity_id, details)
            VALUES (@UserId, 'create_fine', 'borrowing', @BorrowingId,
                    JSON_OBJECT('amount', @Amount, 'reason', @Reason));
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);
        try
        {
            var borrowing = await connection.QuerySingleOrDefaultAsync<long?>(
                new CommandDefinition(borrowingSql,
                    new { request.BorrowingId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (borrowing is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            await connection.ExecuteAsync(new CommandDefinition(insertSql,
                new { request.BorrowingId, request.Amount, request.Reason },
                transaction: transaction, cancellationToken: cancellationToken));
            var fineId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition("SELECT CAST(LAST_INSERT_ID() AS SIGNED);",
                    transaction: transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new
                {
                    UserId = actorUserId,
                    request.BorrowingId,
                    request.Amount,
                    request.Reason
                },
                transaction: transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new LibrarianActionResult(fineId, "unpaid");
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<LibrarianActionResult?> RecordPaymentAsync(
        long actorUserId, RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        const string fineSql = """
            SELECT amount
            FROM fines
            WHERE fine_id = @FineId
            FOR UPDATE;
            """;
        const string insertSql = """
            INSERT INTO payments
                (fine_id, amount_paid, payment_method, receipt_reference, recorded_by)
            VALUES (@FineId, @AmountPaid, @PaymentMethod, @ReceiptReference, @UserId);
            """;
        const string statusSql = """
            UPDATE fines f
            SET f.status = CASE
                WHEN (SELECT COALESCE(SUM(p.amount_paid), 0)
                      FROM payments p WHERE p.fine_id = f.fine_id) >= f.amount
                    THEN 'paid'
                ELSE 'partially_paid'
            END,
            f.settled_at = CASE
                WHEN (SELECT COALESCE(SUM(p.amount_paid), 0)
                      FROM payments p WHERE p.fine_id = f.fine_id) >= f.amount
                    THEN CURRENT_TIMESTAMP
                ELSE NULL
            END
            WHERE f.fine_id = @FineId;
            """;
        const string logSql = """
            INSERT INTO transaction_logs
                (user_id, action, entity_type, entity_id, details)
            VALUES (@UserId, 'record_payment', 'fine', @FineId,
                    JSON_OBJECT('amount_paid', @AmountPaid,
                               'receipt_reference', @ReceiptReference));
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);
        try
        {
            var fineAmount = await connection.QuerySingleOrDefaultAsync<decimal?>(
                new CommandDefinition(fineSql, new { request.FineId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (fineAmount is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            await connection.ExecuteAsync(new CommandDefinition(insertSql,
                new
                {
                    request.FineId,
                    request.AmountPaid,
                    request.PaymentMethod,
                    request.ReceiptReference,
                    UserId = actorUserId
                },
                transaction: transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(statusSql,
                new { request.FineId },
                transaction: transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new
                {
                    UserId = actorUserId,
                    request.FineId,
                    request.AmountPaid,
                    request.ReceiptReference
                },
                transaction: transaction, cancellationToken: cancellationToken));
            var status = await connection.ExecuteScalarAsync<string>(
                new CommandDefinition(
                    "SELECT status FROM fines WHERE fine_id = @FineId;",
                    new { request.FineId }, transaction: transaction,
                    cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new LibrarianActionResult(
                request.FineId,
                status ?? throw new InvalidOperationException(
                    "Fine status was not available after recording payment."));
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
