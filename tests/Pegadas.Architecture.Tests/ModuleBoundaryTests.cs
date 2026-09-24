using System.Reflection;
using NetArchTest.Rules;
using Pegadas.BuildingBlocks.Modules;
using Pegadas.SharedKernel.Results;

namespace Pegadas.Architecture.Tests;

/// <summary>
/// Enforces the modular-monolith rules of ADR-0001. Module assemblies are discovered from the
/// test output, so a new module is covered without touching these tests.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string ModulePrefix = "Pegadas.Modules.";
    private const string ContractsSuffix = ".Contracts";

    private static readonly Assembly[] ModuleAssemblies = LoadAssemblies($"{ModulePrefix}*.dll");

    private static IEnumerable<Assembly> Implementations =>
        ModuleAssemblies.Where(a => !a.GetName().Name!.EndsWith(ContractsSuffix, StringComparison.Ordinal));

    private static IEnumerable<Assembly> Contracts =>
        ModuleAssemblies.Where(a => a.GetName().Name!.EndsWith(ContractsSuffix, StringComparison.Ordinal));

    [Fact]
    public void All_modules_are_discovered()
    {
        string[] expected = ["Audit", "Diary", "Identity", "Notifications", "Organization", "Summaries"];

        var found = Implementations.Select(ModuleName).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, found);
    }

    [Fact]
    public void Modules_only_reference_other_modules_through_their_contracts()
    {
        var violations = new List<string>();
        foreach (var assembly in ModuleAssemblies)
        {
            var owner = ModuleName(assembly);
            foreach (var reference in assembly.GetReferencedAssemblies().Select(r => r.Name!))
            {
                var isOtherModule = reference.StartsWith(ModulePrefix, StringComparison.Ordinal)
                    && ModuleName(reference) != owner;
                if (isOtherModule && !reference.EndsWith(ContractsSuffix, StringComparison.Ordinal))
                {
                    violations.Add($"{assembly.GetName().Name} -> {reference}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Contracts_do_not_depend_on_infrastructure()
    {
        string[] forbidden =
        [
            "Pegadas.BuildingBlocks",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "Hangfire",
        ];

        foreach (var contracts in Contracts)
        {
            var result = Types.InAssembly(contracts).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

            Assert.True(result.IsSuccessful, Describe(contracts, result));
        }
    }

    [Fact]
    public void The_module_entry_point_is_the_only_public_type_of_an_implementation()
    {
        foreach (var implementation in Implementations)
        {
            var result = Types.InAssembly(implementation)
                .That().ArePublic()
                .Should().ImplementInterface(typeof(IModule))
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(implementation, result));
            Assert.Single(implementation.GetExportedTypes(), t => typeof(IModule).IsAssignableFrom(t));
        }
    }

    [Fact]
    public void Shared_kernel_has_no_infrastructure_dependencies()
    {
        var result = Types.InAssembly(typeof(Result).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Pegadas.BuildingBlocks",
                "Pegadas.Modules",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql",
                "Hangfire")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(typeof(Result).Assembly, result));
    }

    [Fact]
    public void Building_blocks_do_not_depend_on_modules()
    {
        var result = Types.InAssembly(typeof(IModule).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Pegadas.Modules")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(typeof(IModule).Assembly, result));
    }

    private static string ModuleName(Assembly assembly) => ModuleName(assembly.GetName().Name!);

    // "Pegadas.Modules.Diary.Contracts" -> "Diary"
    private static string ModuleName(string assemblyName) =>
        assemblyName[ModulePrefix.Length..].Split('.')[0];

    private static Assembly[] LoadAssemblies(string pattern) =>
        Directory.GetFiles(AppContext.BaseDirectory, pattern)
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .ToArray();

    private static string Describe(Assembly assembly, NetArchTest.Rules.TestResult result) =>
        $"{assembly.GetName().Name}: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
