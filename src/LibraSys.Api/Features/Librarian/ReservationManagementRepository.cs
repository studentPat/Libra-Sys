using Dapper;
using LibraSys.Api.Data;
using MySqlConnector;

namespace LibraSys.Api.Features.Librarian;

public sealed class ReservationManagementRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<ExpireDueReservationsResult> ExpireDueAsync(
        long actorUserId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string dueSql = """
                SELECT CAST(reservation_id AS SIGNED)
                FROM reservations
                WHERE status IN ('queued', 'ready')
                  AND expires_at IS NOT NULL
                  AND expires_at <= CURRENT_TIMESTAMP
                FOR UPDATE;
                """;
            var reservationIds = (await connection.QueryAsync<long>(new CommandDefinition(
                dueSql, transaction: transaction, cancellationToken: cancellationToken))).AsList();

            const string updateSql = """
                UPDATE reservations
                SET status = 'expired',
                    expires_at = COALESCE(expires_at, CURRENT_TIMESTAMP)
                WHERE reservation_id = @ReservationId;
                """;
            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'expire_reservation', 'reservation', @ReservationId,
                        JSON_OBJECT('status', 'expired', 'source', 'batch'));
                """;
            foreach (var reservationId in reservationIds)
            {
                await connection.ExecuteAsync(new CommandDefinition(updateSql,
                    new { ReservationId = reservationId },
                    transaction: transaction, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(logSql,
                    new { UserId = actorUserId, ReservationId = reservationId },
                    transaction: transaction, cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return new ExpireDueReservationsResult(reservationIds.Count);
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<ReservationManagementResult?> MarkReadyAsync(
        long actorUserId, long reservationId, CancellationToken cancellationToken) =>
        TransitionAsync(actorUserId, reservationId, "ready",
            "queued", "Reservation is not queued.", cancellationToken);

    public Task<ReservationManagementResult?> FulfillAsync(
        long actorUserId, long reservationId, CancellationToken cancellationToken) =>
        TransitionAsync(actorUserId, reservationId, "fulfilled",
            "ready", "Reservation is not ready for fulfillment.", cancellationToken);

    public Task<ReservationManagementResult?> ExpireAsync(
        long actorUserId, long reservationId, CancellationToken cancellationToken) =>
        TransitionAsync(actorUserId, reservationId, "expired",
            "queued", "Reservation is not active.", cancellationToken, "ready");

    private async Task<ReservationManagementResult?> TransitionAsync(
        long actorUserId, long reservationId, string targetStatus,
        string requiredStatus, string conflictMessage, CancellationToken cancellationToken,
        string? alternateRequiredStatus = null)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string reservationSql = """
                SELECT CAST(reservation_id AS SIGNED) AS ReservationId,
                       status AS Status
                FROM reservations
                WHERE reservation_id = @ReservationId
                FOR UPDATE;
                """;
            var reservation = await connection.QuerySingleOrDefaultAsync<ReservationRow>(
                new CommandDefinition(reservationSql, new { ReservationId = reservationId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (reservation is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            if (reservation.Status != requiredStatus &&
                reservation.Status != alternateRequiredStatus)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ReservationManagementConflictException(conflictMessage);
            }

            var expirySql = targetStatus == "ready"
                ? ", expires_at = DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 2 DAY)"
                : targetStatus == "fulfilled"
                    ? ", fulfilled_at = CURRENT_TIMESTAMP"
                    : ", expires_at = COALESCE(expires_at, CURRENT_TIMESTAMP)";
            var updateSql = $"""
                UPDATE reservations
                SET status = @TargetStatus{expirySql}
                WHERE reservation_id = @ReservationId;
                """;
            await connection.ExecuteAsync(new CommandDefinition(updateSql,
                new { ReservationId = reservationId, TargetStatus = targetStatus },
                transaction: transaction, cancellationToken: cancellationToken));

            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'manage_reservation', 'reservation', @ReservationId,
                        JSON_OBJECT('status', @Status));
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new { UserId = actorUserId, ReservationId = reservationId, Status = targetStatus },
                transaction: transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new ReservationManagementResult(reservationId, targetStatus);
        }
        catch (ReservationManagementConflictException)
        {
            throw;
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private sealed class ReservationRow
    {
        public long ReservationId { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}

public sealed class ReservationManagementConflictException(string message) : Exception(message);
