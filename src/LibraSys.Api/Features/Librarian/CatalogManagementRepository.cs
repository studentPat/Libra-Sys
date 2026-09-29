using Dapper;
using LibraSys.Api.Data;
using MySqlConnector;

namespace LibraSys.Api.Features.Librarian;

public sealed class CatalogManagementRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<CatalogManagementResult?> CreateBookAsync(
        long actorUserId, CreateBookRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string insertSql = """
                INSERT INTO books (isbn, title, publisher, publication_year)
                VALUES (@Isbn, @Title, @Publisher, @PublicationYear);
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertSql, request,
                transaction: transaction, cancellationToken: cancellationToken));
            var bookId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition("SELECT CAST(LAST_INSERT_ID() AS SIGNED);",
                    transaction: transaction, cancellationToken: cancellationToken));

            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'create_book', 'book', @BookId,
                        JSON_OBJECT('isbn', @Isbn, 'title', @Title));
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new { UserId = actorUserId, BookId = bookId, request.Isbn, request.Title },
                transaction: transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new CatalogManagementResult(bookId, "created");
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<CatalogManagementResult?> AddCopyAsync(
        long actorUserId, AddCopyRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string bookSql = """
                SELECT book_id FROM books WHERE book_id = @BookId FOR UPDATE;
                """;
            var bookId = await connection.QuerySingleOrDefaultAsync<long?>(
                new CommandDefinition(bookSql, new { request.BookId },
                    transaction: transaction, cancellationToken: cancellationToken));
            if (bookId is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string insertSql = """
                INSERT INTO book_copies (book_id, accession_number, item_condition)
                VALUES (@BookId, @AccessionNumber, @ItemCondition);
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertSql, request,
                transaction: transaction, cancellationToken: cancellationToken));
            var copyId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition("SELECT CAST(LAST_INSERT_ID() AS SIGNED);",
                    transaction: transaction, cancellationToken: cancellationToken));
            const string logSql = """
                INSERT INTO transaction_logs
                    (user_id, action, entity_type, entity_id, details)
                VALUES (@UserId, 'add_copy', 'copy', @CopyId,
                        JSON_OBJECT('book_id', @BookId,
                                    'accession_number', @AccessionNumber));
                """;
            await connection.ExecuteAsync(new CommandDefinition(logSql,
                new { UserId = actorUserId, CopyId = copyId, request.BookId, request.AccessionNumber },
                transaction: transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new CatalogManagementResult(copyId, "created");
        }
        catch (MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
