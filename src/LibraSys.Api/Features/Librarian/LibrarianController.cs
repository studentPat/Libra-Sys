using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace LibraSys.Api.Features.Librarian;

[ApiController]
[Authorize(Policy = "LibrarianOnly")]
[Route("api/librarian")]
public sealed class LibrarianController(LibrarianRepository repository) : ControllerBase
{
    [HttpPost("fines")]
    public async Task<ActionResult<LibrarianActionResult>> CreateFine(
        CreateFineRequest request, CancellationToken cancellationToken)
    {
        var reasons = new[] { "overdue", "lost", "damaged", "other" };
        if (request.BorrowingId <= 0 || request.Amount <= 0 ||
            !reasons.Contains(request.Reason, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "A valid borrowing, positive amount, and fine reason are required.", reasons });
        }

        var result = await repository.CreateFineAsync(
            GetUserId(), request with { Reason = request.Reason.ToLowerInvariant() },
            cancellationToken);
        return result is null
            ? NotFound(new { error = "Borrowing not found." })
            : Ok(result);
    }

    [HttpPost("payments")]
    public async Task<ActionResult<LibrarianActionResult>> RecordPayment(
        RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        var methods = new[] { "cash", "bank_transfer", "other" };
        if (request.FineId <= 0 || request.AmountPaid <= 0 ||
            string.IsNullOrWhiteSpace(request.ReceiptReference) ||
            !methods.Contains(request.PaymentMethod, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "A valid fine, positive amount, payment method, and receipt reference are required.", methods });
        }

        try
        {
            var result = await repository.RecordPaymentAsync(
                GetUserId(),
                request with { PaymentMethod = request.PaymentMethod.ToLowerInvariant() },
                cancellationToken);
            return result is null
                ? NotFound(new { error = "Fine not found." })
                : Ok(result);
        }
        catch (MySqlException ex) when (ex.Number is 1644 or 1062)
        {
            return Conflict(new { error = "Payment was rejected because it exceeds the balance or uses a duplicate receipt reference." });
        }
    }

    private long GetUserId() =>
        long.Parse(User.FindFirstValue("sub") ?? throw new InvalidOperationException(
            "Authenticated librarian token has no subject."));
}
