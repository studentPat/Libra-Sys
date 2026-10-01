namespace LibraSys.Api.Features.Librarian;

public sealed record LibrarianMember(
    long MemberId,
    long UserId,
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string? ContactInfo,
    string Status);

public sealed record UpdateMemberStatusRequest(string Status);

public sealed record UpdateMemberRequest(
    string FirstName,
    string LastName,
    string Email,
    string? ContactInfo);

public sealed record MemberManagementResult(long MemberId, string Status);

public sealed record LibrarianMemberBorrowing(
    long BorrowingId,
    long MemberId,
    string Title,
    string AccessionNumber,
    DateTime BorrowDate,
    DateTime DueDate,
    DateTime? ReturnDate,
    string Status,
    string? ReturnCondition);

public sealed record LibrarianMemberFine(
    long FineId,
    long BorrowingId,
    decimal Amount,
    string Reason,
    string Status,
    DateTime CreatedAt,
    decimal PaidAmount,
    decimal RemainingAmount);

public sealed record LibrarianMemberPayment(
    long PaymentId,
    long FineId,
    decimal AmountPaid,
    DateTime PaymentDate,
    string PaymentMethod,
    string ReceiptReference);
