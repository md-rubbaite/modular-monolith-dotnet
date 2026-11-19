using System.Collections.Concurrent;
using System.Globalization;
using BuildingBlocks;
using Billing.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Contracts;

namespace Billing.Core;

public sealed class BillingModule : IModule
{
    public string Name => "billing";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<InvoiceStore>();
        services.AddSingleton<IBillingApi, BillingApi>();
        services.AddSingleton<IIntegrationEventConsumer<OrderPlaced>, IssueInvoiceOnOrderPlaced>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/billing/orders/{orderId:guid}/invoice", async (Guid orderId, IBillingApi billing, CancellationToken ct) =>
            await billing.GetInvoiceForOrderAsync(orderId, ct) is { } invoice ? Results.Ok(invoice) : Results.NotFound())
            .WithTags("Billing");
    }
}

/// <summary>Owned exclusively by the Billing module (schema "billing" in a real database).</summary>
internal sealed class InvoiceStore
{
    private readonly ConcurrentDictionary<Guid, InvoiceSummary> _byOrder = new();
    private int _sequence;

    /// <summary>Idempotent: a redelivered event for the same order returns the existing invoice.</summary>
    public InvoiceSummary GetOrAdd(Guid orderId, Func<int, InvoiceSummary> create) =>
        _byOrder.GetOrAdd(orderId, _ => create(Interlocked.Increment(ref _sequence)));

    public InvoiceSummary? Find(Guid orderId) => _byOrder.GetValueOrDefault(orderId);

    public int Count => _byOrder.Count;
}

internal sealed class IssueInvoiceOnOrderPlaced(InvoiceStore invoices) : IIntegrationEventConsumer<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced integrationEvent, CancellationToken ct)
    {
        invoices.GetOrAdd(integrationEvent.OrderId, seq => new InvoiceSummary(
            $"INV-{integrationEvent.OccurredAt:yyyy}-{seq.ToString("D5", CultureInfo.InvariantCulture)}",
            integrationEvent.OrderId,
            integrationEvent.TotalMinor,
            integrationEvent.Currency,
            DateOnly.FromDateTime(integrationEvent.OccurredAt.UtcDateTime).AddDays(14)));
        return Task.CompletedTask;
    }
}

internal sealed class BillingApi(InvoiceStore invoices) : IBillingApi
{
    public Task<InvoiceSummary?> GetInvoiceForOrderAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult(invoices.Find(orderId));
}
