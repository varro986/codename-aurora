using CodenameAurora.Core;
using Xunit;

namespace CodenameAurora.Tests.Unit;

public sealed class PlaceholderTest
{
    [Fact]
    public void PipelineReady_Returns_True() => Assert.True(CorePlaceholder.PipelineReady());
}
