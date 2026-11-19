using System.Reflection;
using Billing.Core;
using BuildingBlocks;
using Catalog.Core;
using Orders.Core;

namespace ModularMonolith.Tests;

/// <summary>
/// Module boundaries enforced in CI. A pull request that makes one module reach into another
/// module's internals fails the build instead of quietly turning the monolith into a big ball of mud.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] ModuleNames = ["Catalog", "Orders", "Billing"];

    private static readonly Assembly[] CoreAssemblies =
        [typeof(CatalogModule).Assembly, typeof(OrdersModule).Assembly, typeof(BillingModule).Assembly];

    private static IEnumerable<string> ReferencedProjectAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n == "BuildingBlocks" || ModuleNames.Any(m => n.StartsWith(m + ".", StringComparison.Ordinal)));

    [Fact]
    public void A_module_never_references_another_modules_Core()
    {
        foreach (var core in CoreAssemblies)
        {
            var self = core.GetName().Name!;
            var illegal = ReferencedProjectAssemblies(core)
                .Where(n => n.EndsWith(".Core", StringComparison.Ordinal) && n != self)
                .ToList();

            Assert.True(illegal.Count == 0, $"{self} references {string.Join(", ", illegal)}. Depend on the other module's Contracts instead.");
        }
    }

    [Fact]
    public void Contracts_depend_only_on_BuildingBlocks()
    {
        foreach (var name in ModuleNames)
        {
            var contracts = Assembly.Load($"{name}.Contracts");
            var refs = ReferencedProjectAssemblies(contracts).Where(n => n != "BuildingBlocks").ToList();

            Assert.True(refs.Count == 0, $"{name}.Contracts references {string.Join(", ", refs)}.");
        }
    }

    [Fact]
    public void Module_internals_are_not_public()
    {
        foreach (var core in CoreAssemblies)
        {
            var leaked = core.GetExportedTypes()
                .Where(t => !typeof(IModule).IsAssignableFrom(t))
                .Select(t => t.FullName)
                .ToList();

            Assert.True(leaked.Count == 0, $"{core.GetName().Name} exposes {string.Join(", ", leaked)}. Only the IModule entry point may be public.");
        }
    }

    [Fact]
    public void BuildingBlocks_knows_nothing_about_business_modules()
    {
        var refs = ReferencedProjectAssemblies(typeof(IModule).Assembly).ToList();

        Assert.Empty(refs);
    }
}
