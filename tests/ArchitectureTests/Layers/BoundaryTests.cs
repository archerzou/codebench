using System.Reflection;
using Shouldly;

namespace ArchitectureTests.Layers;

/// <summary>
/// Security boundaries of the grading pipeline (docs/guide/03-worker-and-sandbox.md):
/// the worker never gets database access, and the shared contracts stay BCL-only.
/// Checked on referenced assemblies so a transitive package reference is caught too.
/// </summary>
public class BoundaryTests : BaseTest
{
    private static readonly string[] DatabaseAssemblies =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
    ];

    [Fact]
    public void Worker_Should_NotReference_DatabaseOrApi()
    {
        string[] referenced = ReferencedAssemblyNames(WorkerAssembly);

        referenced.ShouldNotContain(name =>
            DatabaseAssemblies.Any(db => name.StartsWith(db, StringComparison.Ordinal)) ||
            name == WebApiAssembly.GetName().Name);
    }

    [Fact]
    public void Contracts_Should_OnlyReference_Bcl()
    {
        string[] referenced = ReferencedAssemblyNames(ContractsAssembly);

        referenced.ShouldAllBe(name =>
            name == "System.Runtime" ||
            name.StartsWith("System.", StringComparison.Ordinal) ||
            name == "netstandard");
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)];
}
