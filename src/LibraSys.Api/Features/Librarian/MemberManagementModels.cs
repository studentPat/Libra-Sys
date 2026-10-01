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
