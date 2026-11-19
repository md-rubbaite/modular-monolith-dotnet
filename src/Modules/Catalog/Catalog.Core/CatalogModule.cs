using BuildingBlocks;
using Catalog.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Core;

public sealed class CatalogModule : IModule
{
    public string Name => "catalog";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ProductStore>();
        services.AddSingleton<ICatalogApi, CatalogApi>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/catalog").WithTags("Catalog");
        group.MapGet("/products", (ProductStore store) => Results.Ok(store.All()));
    }
}

internal sealed record Product(string Sku, string Name, long UnitPriceMinor, string Currency);

/// <summary>Owned exclusively by the Catalog module (schema "catalog" in a real database).</summary>
internal sealed class ProductStore
{
    private readonly Dictionary<string, Product> _products = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ARCH-REVIEW"] = new("ARCH-REVIEW", "Architecture review", 90_000, "USD"),
        ["CLOUD-AUDIT"] = new("CLOUD-AUDIT", "Cloud cost & security audit", 110_000, "USD"),
        ["SUPPORT-HR"] = new("SUPPORT-HR", "Engineering support hour", 6_000, "USD"),
    };

    public IReadOnlyCollection<Product> All() => _products.Values;

    public Product? Find(string sku) => _products.GetValueOrDefault(sku);
}

internal sealed class CatalogApi(ProductStore store) : ICatalogApi
{
    public Task<ProductPrice?> GetPriceAsync(string sku, CancellationToken ct)
    {
        var p = store.Find(sku);
        return Task.FromResult(p is null ? null : new ProductPrice(p.Sku, p.Name, p.UnitPriceMinor, p.Currency));
    }
}
