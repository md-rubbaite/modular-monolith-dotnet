namespace Catalog.Contracts;

public sealed record ProductPrice(string Sku, string Name, long UnitPriceMinor, string Currency);

/// <summary>The only way other modules may read catalog data. No shared tables, no shared DbContext.</summary>
public interface ICatalogApi
{
    Task<ProductPrice?> GetPriceAsync(string sku, CancellationToken ct);
}
