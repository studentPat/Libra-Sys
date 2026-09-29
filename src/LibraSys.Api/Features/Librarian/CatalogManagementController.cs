using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace LibraSys.Api.Features.Librarian;

[ApiController]
[Authorize(Policy = "LibrarianOnly")]
[Route("api/librarian/catalog")]
public sealed class CatalogManagementController(
    CatalogManagementRepository repository) : ControllerBase
{
    [HttpPost("books")]
    public async Task<ActionResult<CatalogManagementResult>> CreateBook(
        CreateBookRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Isbn) ||
            string.IsNullOrWhiteSpace(request.Title) ||
            request.Isbn.Length > 20 || request.Title.Length > 255 ||
            request.PublicationYear is < 1000 or > 9999)
        {
            return BadRequest(new { error = "ISBN and title are required; values must fit the database fields." });
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await repository.CreateBookAsync(userId.Value, request, cancellationToken));
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { error = "A book with this ISBN already exists." });
        }
    }

    [HttpPost("copies")]
    public async Task<ActionResult<CatalogManagementResult>> AddCopy(
        AddCopyRequest request, CancellationToken cancellationToken)
    {
        var conditions = new[] { "new", "good", "fair", "damaged" };
        if (request.BookId <= 0 || string.IsNullOrWhiteSpace(request.AccessionNumber) ||
            request.AccessionNumber.Length > 40 ||
            !conditions.Contains(request.ItemCondition, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "A valid book, accession number, and item condition are required.", conditions });
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await repository.AddCopyAsync(
                userId.Value,
                request with { ItemCondition = request.ItemCondition.ToLowerInvariant() },
                cancellationToken);
            return result is null ? NotFound(new { error = "Book not found." }) : Ok(result);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { error = "This accession number already exists." });
        }
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : null;
    }
}
