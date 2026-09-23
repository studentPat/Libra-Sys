namespace LibraSys.Api.Features.Catalog;

public sealed record BookDetails(
    long BookId,
    string Isbn,
    string Title,
    string? Publisher,
    int? PublicationYear,
    IReadOnlyList<string> Authors,
    IReadOnlyList<string> Categories,
    long AvailableCopyCount);
