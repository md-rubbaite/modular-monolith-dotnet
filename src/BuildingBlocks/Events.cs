using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks;

/// <summary>A fact published by one module for others. Lives in the publisher's Contracts project.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct);
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent;
}

/// <summary>
/// In-process bus. Handlers run after the publisher's work is done. When a module is extracted into
/// its own service, swap this for an outbox + broker (Azure Service Bus, RabbitMQ) with no change to
/// the publishing or handling code.
/// </summary>
public sealed class InProcessEventBus(IServiceProvider services) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        foreach (var handler in services.GetServices<IIntegrationEventConsumer<TEvent>>())
        {
            await handler.HandleAsync(integrationEvent, ct);
        }
    }
}
