namespace LibraSys.Api.Features.Members;

public sealed record MemberProfile(
    long MemberId,
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? ContactInfo,
    string Status);

public sealed record MemberBorrowing(
    long BorrowingId,
    string Title,
    string AccessionNumber,
    DateTime BorrowDate,
    DateTime DueDate,
    DateTime? ReturnDate,
    string Status,
    string? ReturnCondition);

public sealed record UpdateMemberProfileRequest(
    string FirstName,
    string LastName,
    string Email,
    string? ContactInfo);

public sealed record BorrowRequest(long CopyId);

public sealed record ReturnRequest(string ReturnCondition);

public sealed record BorrowingActionResult(
    long BorrowingId,
    DateTime DueDate,
    string Status);

public sealed record ReservationRequest(long BookId);

public sealed record MemberReservation(
    long ReservationId,
    long BookId,
    string Title,
    DateTime ReservedAt,
    DateTime? ExpiresAt,
    string Status);

public sealed record ReservationActionResult(
    long ReservationId,
    string Status);
