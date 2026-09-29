namespace LibraSys.Api.Features.Librarian;

public sealed record CreateFineRequest(
    long BorrowingId,
    decimal Amount,
    string Reason);

public sealed record RecordPaymentRequest(
    long FineId,
    decimal AmountPaid,
    string PaymentMethod,
    string ReceiptReference);

public sealed record LibrarianActionResult(long Id, string Status);
