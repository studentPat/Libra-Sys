namespace LibraSys.Api.Features.Librarian;

public sealed record CreateBookRequest(
    string Isbn,
    string Title,
    string? Publisher,
    int? PublicationYear);

public sealed record AddCopyRequest(
    long BookId,
    string AccessionNumber,
    string ItemCondition = "good");

public sealed record UpdateBookRequest(
    string Isbn,
    string Title,
    string? Publisher,
    int? PublicationYear);

public sealed record CatalogManagementResult(long Id, string Status);

public sealed class CatalogManagementConflictException(string message) : Exception(message);
