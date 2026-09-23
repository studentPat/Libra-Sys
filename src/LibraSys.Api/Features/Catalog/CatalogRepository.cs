using Dapper;
using LibraSys.Api.Data;

namespace LibraSys.Api.Features.Catalog;

public sealed class CatalogRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<CatalogItem>> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;
        const string sql = """
            SELECT b.book_id AS BookId, b.isbn AS Isbn, b.title AS Title,
                   b.publisher AS Publisher, b.publication_year AS PublicationYear,
                   COUNT(bc.copy_id) AS AvailableCopyCount
            FROM books b
            LEFT JOIN book_copies bc
              ON bc.book_id = b.book_id AND bc.status = 'available'
            WHERE @Search IS NULL
               OR b.title LIKE CONCAT('%', @Search, '%')
               OR b.isbn LIKE CONCAT('%', @Search, '%')
            GROUP BY b.book_id, b.isbn, b.title, b.publisher, b.publication_year
            ORDER BY b.title, b.book_id
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<CatalogItem>(
            new CommandDefinition(sql, new { Search = search, PageSize = pageSize, Offset = offset },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<BookDetails?> GetByIdAsync(long bookId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT b.book_id AS BookId, b.isbn AS Isbn, b.title AS Title,
                   b.publisher AS Publisher, b.publication_year AS PublicationYear,
                   COUNT(DISTINCT CASE WHEN bc.status = 'available' THEN bc.copy_id END)
                       AS AvailableCopyCount,
                   GROUP_CONCAT(DISTINCT a.name ORDER BY a.name SEPARATOR '|') AS AuthorNames,
                   GROUP_CONCAT(DISTINCT c.category_name ORDER BY c.category_name SEPARATOR '|')
                       AS CategoryNames
            FROM books b
            LEFT JOIN book_copies bc ON bc.book_id = b.book_id
            LEFT JOIN book_authors ba ON ba.book_id = b.book_id
            LEFT JOIN authors a ON a.author_id = ba.author_id
            LEFT JOIN book_categories bcat ON bcat.book_id = b.book_id
            LEFT JOIN categories c ON c.category_id = bcat.category_id
            WHERE b.book_id = @BookId
            GROUP BY b.book_id, b.isbn, b.title, b.publisher, b.publication_year;
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<BookDetailsRow>(
            new CommandDefinition(sql, new { BookId = bookId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new BookDetails(row.BookId, row.Isbn, row.Title, row.Publisher, row.PublicationYear,
                Split(row.AuthorNames), Split(row.CategoryNames), row.AvailableCopyCount);
    }

    private static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record BookDetailsRow(
        long BookId, string Isbn, string Title, string? Publisher, int? PublicationYear,
        string? AuthorNames, string? CategoryNames, long AvailableCopyCount);
}
