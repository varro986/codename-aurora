using NetArchTest.Rules;
using Xunit;

namespace Aurora.Tests.Architecture;

public class ModuleDependencyTests
{
    private const string OcrNamespace = "Aurora.OCR";
    private const string TranslationNamespace = "Aurora.Translation";
    private const string UiNamespace = "Aurora.UI";
    private const string AdminNamespace = "Aurora.Admin";

    [Fact, Trait("Category", "Architecture")]
    public void CoreModule_MustNotDependOn_AnyOperationalModule()
    {
        var result = Types.InAssembly(typeof(Aurora.Core.Interfaces.IOcrService).Assembly)
            .Should().NotHaveDependencyOnAny(OcrNamespace, TranslationNamespace, UiNamespace, AdminNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact, Trait("Category", "Architecture")]
    public void OcrModule_MustNotDependOn_OtherOperationalModules()
    {
        var result = Types.InAssembly(typeof(Aurora.OCR.OcrService).Assembly)
            .Should().NotHaveDependencyOnAny(TranslationNamespace, UiNamespace, AdminNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact, Trait("Category", "Architecture")]
    public void TranslationModule_MustNotDependOn_OtherOperationalModules()
    {
        var result = Types.InAssembly(typeof(Aurora.Translation.TranslationEngine).Assembly)
            .Should().NotHaveDependencyOnAny(OcrNamespace, UiNamespace, AdminNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact, Trait("Category", "Architecture")]
    public void UiModule_MustNotDependOn_OtherOperationalModules()
    {
        var result = Types.InAssembly(typeof(Aurora.UI.App).Assembly)
            .Should().NotHaveDependencyOnAny(OcrNamespace, TranslationNamespace, AdminNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact, Trait("Category", "Architecture")]
    public void AdminModule_MustNotDependOn_OtherOperationalModules()
    {
        var result = Types.InAssembly(typeof(Aurora.Admin.AdminService).Assembly)
            .Should().NotHaveDependencyOnAny(OcrNamespace, TranslationNamespace, UiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact, Trait("Category", "Architecture")]
    public void ModelManager_MustBeImplementedOnly_ByTranslationModule()
    {
        // archi.md §5 Model Manager: IModelManager is an Aurora.Core contract; only Aurora.Translation may implement it.
        var result = Types.InCurrentDomain()
            .That().ImplementInterface(typeof(Aurora.Core.Interfaces.IModelManager))
            .Should().ResideInNamespace(TranslationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    private static string FormatFailure(TestResult result) =>
        $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
