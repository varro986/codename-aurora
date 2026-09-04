#nullable enable
using NetArchTest.Rules;
using Xunit;

namespace CodenameAurora.Tests.Architecture;

/// <summary>Enforces module isolation rules via NetArchTest. One test per architectural law.</summary>
public sealed class ModuleIsolationTests
{
    private const string CoreNamespace = "CodenameAurora.Core";
    private const string AppNamespace = "CodenameAurora.App";

    private static readonly string[] OperationalModules =
    [
        "CodenameAurora.OCR",
        "CodenameAurora.Translation",
        "CodenameAurora.UI",
        "CodenameAurora.Admin",
    ];

    [Trait("Category", "Architecture")]
    [Fact]
    public void Core_must_not_reference_any_other_module()
    {
        var forbidden = OperationalModules.Append(AppNamespace).ToArray();
        var result = Types
            .InNamespace(CoreNamespace)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Trait("Category", "Architecture")]
    [Fact]
    public void Operational_modules_must_not_reference_each_other()
    {
        foreach (var module in OperationalModules)
        {
            var siblings = OperationalModules.Where(m => m != module).ToArray();
            var result = Types
                .InNamespace(module)
                .Should()
                .NotHaveDependencyOnAny(siblings)
                .GetResult();

            Assert.True(result.IsSuccessful, $"{module}: {FormatFailures(result)}");
        }
    }

    private static string FormatFailures(TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
