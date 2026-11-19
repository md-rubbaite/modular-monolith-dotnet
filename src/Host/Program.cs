using Billing.Core;
using BuildingBlocks;
using Catalog.Core;
using Orders.Core;

var builder = WebApplication.CreateBuilder(args);

// One deployable, explicit module list. Extracting a module later means moving one entry
// to another host and replacing in-process calls/events with HTTP/messaging adapters.
IModule[] modules = [new CatalogModule(), new OrdersModule(), new BillingModule()];

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
builder.Services.AddProblemDetails();
foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration);
}

var app = builder.Build();
foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.MapGet("/", () => Results.Ok(new { modules = modules.Select(m => m.Name) }));
app.Run();

public partial class Program;
