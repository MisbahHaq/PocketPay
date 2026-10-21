using System.Reflection;
using FluentAssertions;

namespace WalletPlatform.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Application_should_not_depend_on_infrastructure_or_persistence_packages()
    {
        var references = GetReferencedAssemblyNames("WalletPlatform.Application");

        references.Should().NotContain("WalletPlatform.Infrastructure");
        references.Should().NotContain(name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        references.Should().NotContain(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_should_not_depend_on_any_other_platform_assembly()
    {
        var references = GetReferencedAssemblyNames("WalletPlatform.Domain");

        references.Should().NotContain(name => name.StartsWith("WalletPlatform", StringComparison.Ordinal));
    }

    private static IEnumerable<string> GetReferencedAssemblyNames(string assemblyName)
    {
        return Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>();
    }
}
