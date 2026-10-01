namespace LibraSys.Api.Features.Librarian;

public sealed record ReservationManagementResult(long ReservationId, string Status);

public sealed record ExpireDueReservationsResult(int ExpiredCount);
