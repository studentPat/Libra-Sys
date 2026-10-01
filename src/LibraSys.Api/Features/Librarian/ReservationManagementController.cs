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
