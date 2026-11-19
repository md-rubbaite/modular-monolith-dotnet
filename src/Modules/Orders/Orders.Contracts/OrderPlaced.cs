using BuildingBlocks;

namespace Orders.Contracts;

public sealed record OrderLine(string Sku, int Quantity, long UnitPriceMinor);

/// <summary>Published by Orders after an order is accepted. Consumers must be idempotent on <see cref="OrderId"/>.</summary>
public sealed record OrderPlaced(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string CustomerId,
    string Currency,
    long TotalMinor,
    IReadOnlyList<OrderLine> Lines) : IIntegrationEvent;
