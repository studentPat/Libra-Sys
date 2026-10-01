using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraSys.Api.Features.Librarian;

[ApiController]
[Authorize(Policy = "LibrarianOnly")]
[Route("api/librarian/reports")]
public sealed class LibrarianReportsController(
    LibrarianReportsRepository repository) : ControllerBase
{
    [HttpGet("overdue-borrowings")]
    public async Task<ActionResult<IReadOnlyList<OverdueBorrowingReport>>> OverdueBorrowings(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPaging(page, pageSize))
        {
            return BadRequest(new { error = "Page must be positive and pageSize must be between 1 and 100." });
        }

        return Ok(await repository.GetOverdueBorrowingsAsync(
            page, pageSize, cancellationToken));
    }

    [HttpGet("monthly-borrowings")]
    public async Task<ActionResult<IReadOnlyList<MonthlyBorrowingReport>>> MonthlyBorrowings(
        [FromQuery] int year, CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2100)
        {
            return BadRequest(new { error = "Year must be between 2000 and 2100." });
        }

        return Ok(await repository.GetMonthlyBorrowingsAsync(year, cancellationToken));
    }

    [HttpGet("member-balances")]
    public async Task<ActionResult<IReadOnlyList<MemberBalanceReport>>> MemberBalances(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPaging(page, pageSize))
        {
            return BadRequest(new { error = "Page must be positive and pageSize must be between 1 and 100." });
        }

        return Ok(await repository.GetMemberBalancesAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("catalog-availability")]
    public async Task<ActionResult<IReadOnlyList<CatalogAvailabilityReport>>> CatalogAvailability(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPaging(page, pageSize))
        {
            return BadRequest(new { error = "Page must be positive and pageSize must be between 1 and 100." });
        }

        return Ok(await repository.GetCatalogAvailabilityAsync(
            search, page, pageSize, cancellationToken));
    }

    [HttpGet("audit-logs")]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntry>>> AuditLogs(
        [FromQuery] string? action, [FromQuery] string? entityType,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ValidPaging(page, pageSize))
        {
            return BadRequest(new { error = "Page must be positive and pageSize must be between 1 and 100." });
        }

        return Ok(await repository.GetAuditLogsAsync(
            action, entityType, page, pageSize, cancellationToken));
    }

    private static bool ValidPaging(int page, int pageSize) =>
        page >= 1 && pageSize is >= 1 and <= 100;
}
