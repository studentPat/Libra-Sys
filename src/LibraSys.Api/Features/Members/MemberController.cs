using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraSys.Api.Features.Members;

[ApiController]
[Authorize(Policy = "MemberOnly")]
[Route("api/member")]
public sealed class MemberController(MemberRepository repository) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<MemberProfile>> GetProfile(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await repository.GetProfileAsync(userId.Value, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("borrowings")]
    public async Task<ActionResult<IReadOnlyList<MemberBorrowing>>> GetBorrowings(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await repository.GetBorrowingsAsync(userId.Value, cancellationToken));
    }

    [HttpPost("borrowings")]
    public async Task<ActionResult<BorrowingActionResult>> Borrow(
        BorrowRequest request, CancellationToken cancellationToken)
    {
        if (request.CopyId <= 0)
        {
            return BadRequest(new { error = "A valid copy ID is required." });
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await repository.BorrowAsync(
            userId.Value, request.CopyId, cancellationToken);
        return result is null
            ? Conflict(new { error = "The member is inactive or the copy is unavailable." })
            : Ok(result);
    }

    [HttpPost("borrowings/{borrowingId:long}/return")]
    public async Task<ActionResult<BorrowingActionResult>> Return(
        long borrowingId, ReturnRequest request, CancellationToken cancellationToken)
    {
        var allowedConditions = new[] { "new", "good", "fair", "damaged" };
        if (borrowingId <= 0 ||
            !allowedConditions.Contains(request.ReturnCondition,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = "A valid borrowing ID and return condition are required.",
                allowedConditions
            });
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await repository.ReturnAsync(
            userId.Value, borrowingId, request.ReturnCondition.ToLowerInvariant(),
            cancellationToken);
        return result is null
            ? NotFound(new { error = "Active borrowing not found for this member." })
            : Ok(result);
    }

    [HttpGet("reservations")]
    public async Task<ActionResult<IReadOnlyList<MemberReservation>>> GetReservations(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await repository.GetReservationsAsync(userId.Value, cancellationToken));
    }

    [HttpPost("reservations")]
    public async Task<ActionResult<ReservationActionResult>> CreateReservation(
        ReservationRequest request, CancellationToken cancellationToken)
    {
        if (request.BookId <= 0)
        {
            return BadRequest(new { error = "A valid book ID is required." });
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await repository.CreateReservationAsync(
            userId.Value, request.BookId, cancellationToken);
        return result is null
            ? Conflict(new { error = "The member is inactive, the book does not exist, or a reservation is already active." })
            : Ok(result);
    }

    [HttpDelete("reservations/{reservationId:long}")]
    public async Task<IActionResult> CancelReservation(
        long reservationId, CancellationToken cancellationToken)
    {
        if (reservationId <= 0)
        {
            return BadRequest(new { error = "A valid reservation ID is required." });
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var cancelled = await repository.CancelReservationAsync(
            userId.Value, reservationId, cancellationToken);
        return cancelled
            ? NoContent()
            : NotFound(new { error = "Active reservation not found for this member." });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        UpdateMemberProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                error = "First name, last name, and email are required."
            });
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var updated = await repository.UpdateProfileAsync(
            userId.Value, request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : null;
    }
}
