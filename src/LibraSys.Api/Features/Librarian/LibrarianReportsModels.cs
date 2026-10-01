namespace LibraSys.Api.Features.Librarian;

public sealed record MemberBalanceReport(
    long MemberId,
    string FirstName,
    string LastName,
    decimal AssessedFines,
    decimal PaidFines,
    decimal Balance);

public sealed record CatalogAvailabilityReport(
    long BookId,
    string Isbn,
    string Title,
    string? Publisher,
    int? PublicationYear,
    long AvailableCopyCount);

public sealed record MonthlyBorrowingReport(
    int Month,
    long BorrowingCount);

public sealed record OverdueBorrowingReport(
    long BorrowingId,
    long MemberId,
    string FirstName,
    string LastName,
    string Title,
    string AccessionNumber,
    DateTime DueDate,
    long DaysOverdue);

public sealed record AuditLogEntry(
    long TransactionId,
    long? UserId,
    string? Username,
    string Action,
    string EntityType,
    long? EntityId,
    DateTime EventTime,
    bool Success,
    string? Details);
