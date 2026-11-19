using BuildingBlocks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Orders.Core;

public sealed class OrdersModule : IModule
{
    public string Name => "orders";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<OrderStore>();
        services.AddSingleton<PlaceOrderHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", async (PlaceOrderCommand command, PlaceOrderHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(command, ct);
            return result.Order is { } order
                ? Results.Created($"/orders/{order.Id}", order)
                : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
        });

        group.MapGet("/{id:guid}", (Guid id, OrderStore store) =>
            store.Find(id) is { } order ? Results.Ok(order) : Results.NotFound());
    }
}
