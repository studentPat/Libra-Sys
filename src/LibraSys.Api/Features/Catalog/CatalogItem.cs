namespace LibraSys.Api.Features.Catalog;

public sealed record CatalogItem(
    long BookId,
    string Isbn,
    string Title,
    string? Publisher,
    int? PublicationYear,
    long AvailableCopyCount);
