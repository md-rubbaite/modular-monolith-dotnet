using System.Collections.Concurrent;
using BuildingBlocks;
using Catalog.Contracts;
using Orders.Contracts;

namespace Orders.Core;

internal sealed record PlaceOrderCommand(string CustomerId, IReadOnlyList<PlaceOrderLine> Lines);

internal sealed record PlaceOrderLine(string Sku, int Quantity);

internal sealed record Order(Guid Id, string CustomerId, string Currency, long TotalMinor, IReadOnlyList<OrderLine> Lines, DateTimeOffset PlacedAt);

internal sealed record PlaceOrderResult(Order? Order, string? Error);

/// <summary>Owned exclusively by the Orders module (schema "orders" in a real database).</summary>
internal sealed class OrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Add(Order order) => _orders[order.Id] = order;

    public Order? Find(Guid id) => _orders.GetValueOrDefault(id);
}

internal sealed class PlaceOrderHandler(ICatalogApi catalog, OrderStore store, IEventBus bus, TimeProvider clock)
{
    public async Task<PlaceOrderResult> HandleAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerId) || command.Lines.Count == 0)
        {
            return new(null, "An order needs a customer and at least one line.");
        }

        var lines = new List<OrderLine>(command.Lines.Count);
        string? currency = null;
        foreach (var line in command.Lines)
        {
            if (line.Quantity is < 1 or > 1_000)
            {
                return new(null, $"Invalid quantity for {line.Sku}.");
            }

            // Cross-module read goes through Catalog's public contract only.
            var price = await catalog.GetPriceAsync(line.Sku, ct);
            if (price is null)
            {
                return new(null, $"Unknown product {line.Sku}.");
            }

            if (currency is not null && !string.Equals(currency, price.Currency, StringComparison.Ordinal))
            {
                return new(null, "All lines must use the same currency.");
            }

            currency = price.Currency;
            lines.Add(new OrderLine(price.Sku, line.Quantity, price.UnitPriceMinor));
        }

        var order = new Order(Guid.NewGuid(), command.CustomerId, currency!, lines.Sum(l => l.UnitPriceMinor * l.Quantity), lines, clock.GetUtcNow());
        store.Add(order);

        // Other modules react to the fact; Orders does not know who listens.
        await bus.PublishAsync(new OrderPlaced(Guid.NewGuid(), order.PlacedAt, order.Id, order.CustomerId, order.Currency, order.TotalMinor, order.Lines), ct);
        return new(order, null);
    }
}
