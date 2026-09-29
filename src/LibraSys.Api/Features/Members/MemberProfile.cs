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
