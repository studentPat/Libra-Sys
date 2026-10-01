using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraSys.Api.Features.Librarian;

[ApiController]
[Authorize(Policy = "LibrarianOnly")]
[Route("api/librarian/members")]
public sealed class MemberManagementController(
    MemberManagementRepository repository) : ControllerBase
{
    [HttpGet("{memberId:long}/borrowings")]
    public async Task<ActionResult<IReadOnlyList<LibrarianMemberBorrowing>>> Borrowings(
        long memberId, [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var statuses = new[] { "active", "returned", "lost", "cancelled" };
        if (memberId <= 0 ||
            (status is not null && !statuses.Contains(status, StringComparer.OrdinalIgnoreCase)))
        {
            return BadRequest(new { error = "Invalid member ID or borrowing status.", statuses });
        }

        return Ok(await repository.GetBorrowingsAsync(
            memberId, status?.ToLowerInvariant(), cancellationToken));
    }

    [HttpPut("{memberId:long}")]
    public async Task<ActionResult<MemberManagementResult>> Update(
        long memberId, UpdateMemberRequest request, CancellationToken cancellationToken)
    {
        if (memberId <= 0 || string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            request.FirstName.Length > 80 || request.LastName.Length > 80 ||
            request.Email.Length > 255 || request.ContactInfo?.Length > 100)
        {
            return BadRequest(new { error = "Member name and email are required and must fit the database fields." });
        }

        var actorUserId = GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await repository.UpdateAsync(
                actorUserId.Value, memberId, request, cancellationToken);
            return result is null
                ? NotFound(new { error = "Member not found." })
                : Ok(result);
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { error = "That email address is already used by another member." });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LibrarianMember>>> List(
        [FromQuery] string? search, [FromQuery] string? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var statuses = new[] { "active", "suspended", "closed" };
        if (page < 1 || pageSize is < 1 or > 100 ||
            (status is not null && !statuses.Contains(status, StringComparer.OrdinalIgnoreCase)))
        {
            return BadRequest(new { error = "Invalid pagination or member status.", statuses });
        }

        return Ok(await repository.ListAsync(
            search, status?.ToLowerInvariant(), page, pageSize, cancellationToken));
    }

    [HttpPut("{memberId:long}/status")]
    public async Task<ActionResult<MemberManagementResult>> UpdateStatus(
        long memberId, UpdateMemberStatusRequest request, CancellationToken cancellationToken)
    {
        var statuses = new[] { "active", "suspended", "closed" };
        if (memberId <= 0 || !statuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "A valid member ID and status are required.", statuses });
        }

        var actorUserId = GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await repository.UpdateStatusAsync(
            actorUserId.Value, memberId, request.Status.ToLowerInvariant(), cancellationToken);
        return result is null
            ? NotFound(new { error = "Member not found." })
            : Ok(result);
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : null;
    }
}
