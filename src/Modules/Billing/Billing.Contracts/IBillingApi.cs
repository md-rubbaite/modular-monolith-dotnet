namespace Billing.Contracts;

public sealed record InvoiceSummary(string InvoiceNumber, Guid OrderId, long AmountDueMinor, string Currency, DateOnly DueDate);

public interface IBillingApi
{
    Task<InvoiceSummary?> GetInvoiceForOrderAsync(Guid orderId, CancellationToken ct);
}
