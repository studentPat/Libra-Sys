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
