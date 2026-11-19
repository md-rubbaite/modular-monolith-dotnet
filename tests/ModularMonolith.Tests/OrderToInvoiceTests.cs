using Billing.Contracts;
using Billing.Core;
using BuildingBlocks;
using Catalog.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Contracts;
using Orders.Core;

namespace ModularMonolith.Tests;

/// <summary>Exercises the modules together through their public surface, the way the host wires them.</summary>
public sealed class OrderToInvoiceTests : IDisposable
{
    private readonly ServiceProvider _services;

    public OrderToInvoiceTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventBus, InProcessEventBus>();
        var config = new ConfigurationBuilder().Build();
        IModule[] modules = [new CatalogModule(), new OrdersModule(), new BillingModule()];
        foreach (var m in modules)
        {
            m.Register(services, config);
        }

        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task Placing_an_order_issues_exactly_one_invoice_via_an_integration_event()
    {
        var bus = _services.GetRequiredService<IEventBus>();
        var billing = _services.GetRequiredService<IBillingApi>();
        var orderId = Guid.NewGuid();
        var evt = new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, orderId, "cus_1", "USD", 180_000, [new OrderLine("ARCH-REVIEW", 2, 90_000)]);

        await bus.PublishAsync(evt, CancellationToken.None);
        await bus.PublishAsync(evt, CancellationToken.None); // redelivery must not create a second invoice

        var invoice = await billing.GetInvoiceForOrderAsync(orderId, CancellationToken.None);
        Assert.NotNull(invoice);
        Assert.Equal(180_000, invoice!.AmountDueMinor);
        Assert.StartsWith("INV-", invoice.InvoiceNumber, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_invoice_returns_null()
    {
        var billing = _services.GetRequiredService<IBillingApi>();

        Assert.Null(await billing.GetInvoiceForOrderAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
