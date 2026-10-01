using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraSys.Api.Features.Librarian;

[ApiController]
[Authorize(Policy = "LibrarianOnly")]
[Route("api/librarian/reservations")]
public sealed class ReservationManagementController(
    ReservationManagementRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LibrarianReservation>>> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var statuses = new[] { "queued", "ready", "fulfilled", "cancelled", "expired" };
        if (page < 1 || pageSize is < 1 or > 100 ||
            (status is not null && !statuses.Contains(status, StringComparer.OrdinalIgnoreCase)))
        {
            return BadRequest(new { error = "Invalid pagination or reservation status.", statuses });
        }

        return Ok(await repository.ListAsync(
            status?.ToLowerInvariant(), page, pageSize, cancellationToken));
    }

    [HttpPost("expire-due")]
    public async Task<ActionResult<ExpireDueReservationsResult>> ExpireDue(
        CancellationToken cancellationToken)
    {
        var actorUserId = GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        return Ok(await repository.ExpireDueAsync(actorUserId.Value, cancellationToken));
    }

    [HttpPost("{reservationId:long}/ready")]
    public Task<ActionResult<ReservationManagementResult>> MarkReady(
        long reservationId, CancellationToken cancellationToken) =>
        Transition(reservationId, repository.MarkReadyAsync, cancellationToken);

    [HttpPost("{reservationId:long}/fulfill")]
    public Task<ActionResult<ReservationManagementResult>> Fulfill(
        long reservationId, CancellationToken cancellationToken) =>
        Transition(reservationId, repository.FulfillAsync, cancellationToken);

    [HttpPost("{reservationId:long}/expire")]
    public Task<ActionResult<ReservationManagementResult>> Expire(
        long reservationId, CancellationToken cancellationToken) =>
        Transition(reservationId, repository.ExpireAsync, cancellationToken);

    private async Task<ActionResult<ReservationManagementResult>> Transition(
        long reservationId,
        Func<long, long, CancellationToken, Task<ReservationManagementResult?>> operation,
        CancellationToken cancellationToken)
    {
        if (reservationId <= 0)
        {
            return BadRequest(new { error = "A valid reservation ID is required." });
        }

        var actorUserId = GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await operation(actorUserId.Value, reservationId, cancellationToken);
            return result is null
                ? NotFound(new { error = "Reservation not found." })
                : Ok(result);
        }
        catch (ReservationManagementConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : null;
    }
}
