namespace LibraSys.Api.Features.Librarian;

public sealed record ReservationManagementResult(long ReservationId, string Status);

public sealed record ExpireDueReservationsResult(int ExpiredCount);

public sealed record LibrarianReservation(
    long ReservationId,
    long MemberId,
    string MemberName,
    long BookId,
    string Title,
    DateTime ReservedAt,
    DateTime? ExpiresAt,
    string Status);
