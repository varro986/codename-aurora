using Aurora.Core;
using Aurora.Core.Interfaces;

namespace Aurora.Tests.Unit.Core;

public sealed class ContinuousModeControllerTests
{
    private static IContinuousModeController Build(int intervalMs = 100)
        => new ContinuousModeController(new FakeSettings(intervalMs));

    [Fact]
    public void InitialState_IsInactive()
    {
        using var sut = Build();
        Assert.False(sut.IsActive);
    }

    [Fact]
    public void Toggle_Activates()
    {
        using var sut = Build();
        sut.Toggle();
        Assert.True(sut.IsActive);
        sut.Stop();
    }

    [Fact]
    public void Toggle_TwiceMakesInactive()
    {
        using var sut = Build();
        sut.Toggle();
        sut.Toggle();
        Assert.False(sut.IsActive);
    }

    [Fact]
    public void Stop_DeactivatesController()
    {
        using var sut = Build();
        sut.Toggle();
        sut.Stop();
        Assert.False(sut.IsActive);
    }

    [Fact]
    public async Task WhileActive_CaptureTick_Fires()
    {
        using var sut = Build(intervalMs: 50);
        var tcs = new TaskCompletionSource<bool>();
        sut.CaptureTick += (_, _) => tcs.TrySetResult(true);

        sut.Toggle();
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(500));
        sut.Stop();

        Assert.True(tcs.Task.IsCompletedSuccessfully, "CaptureTick did not fire within 500 ms.");
    }

    [Fact]
    public async Task AfterStop_CaptureTick_DoesNotFire()
    {
        using var sut = Build(intervalMs: 50);
        sut.Toggle();
        sut.Stop();

        int count = 0;
        sut.CaptureTick += (_, _) => count++;
        await Task.Delay(200);

        Assert.Equal(0, count);
    }

    private sealed class FakeSettings : IAppSettings
    {
        public FakeSettings(int intervalMs) => RulloSamplingInterval = intervalMs;
        public int RulloSamplingInterval { get; }
        public string SourceLanguage => "it";
        public string TargetLanguage => "en";
        public string HotkeyTrigger => "Alt+F1";
        public string HotkeyRullo => "Alt+F2";
        public string PrivateDictionaryPath => "";
        public string GenericDictionaryPath => "";
        public string ModelCachePath => "";
        public string UpdateChannel => "stable";
        public TimeSpan OverlayDismissTimeout => TimeSpan.FromSeconds(5);
        public string OverlayBackgroundColor => "#CC000000";
        public string OverlayForegroundColor => "#FFFFFFFF";
        public int HoverDwellThreshold => 300;
    }
}
