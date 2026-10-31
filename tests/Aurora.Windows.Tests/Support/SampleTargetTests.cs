using System.Diagnostics;

namespace Aurora.Windows.Tests.Support;

public sealed class SampleTargetTests
{
    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Desktop_target_starts_answers_and_closes()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = await SampleTarget.StartAsync(SampleTarget.Desktop, ["Hello", "World"], ct);
        var id = target.Process.Id;
        await using (target)
        {
            Assert.NotEqual(0, await target.MainWindowAsync(ct));
            Assert.Equal("ok Aurora Target Desktop", await target.SendAsync("title", ct));
        }

        Assert.Throws<ArgumentException>(() => Process.GetProcessById(id));
    }
}
