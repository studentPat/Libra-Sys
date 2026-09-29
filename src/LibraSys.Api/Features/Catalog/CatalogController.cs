using Microsoft.AspNetCore.Mvc;

namespace LibraSys.Api.Features.Catalog;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController(CatalogRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CatalogItem>>> Search(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new { error = "page must be positive and pageSize must be between 1 and 100." });
        }

        return Ok(await repository.SearchAsync(search, page, pageSize, cancellationToken));
    }

    [HttpGet("{bookId:long}")]
    public async Task<ActionResult<BookDetails>> Get(long bookId, CancellationToken cancellationToken)
    {
        var book = await repository.GetByIdAsync(bookId, cancellationToken);
        return book is null ? NotFound() : Ok(book);
    }
}
