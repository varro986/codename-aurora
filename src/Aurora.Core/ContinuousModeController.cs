using Aurora.Core.Interfaces;

namespace Aurora.Core;

public sealed class ContinuousModeController : IContinuousModeController
{
    private readonly System.Timers.Timer _timer;
    private readonly IAppSettings _settings;
    private int _busy; // 0 = idle, 1 = processing — use Interlocked for atomic swap

    public bool IsActive => _timer.Enabled;
    public event EventHandler? CaptureTick;

    public ContinuousModeController(IAppSettings settings)
    {
        _settings = settings;
        _timer = new System.Timers.Timer(settings.RulloSamplingInterval) { AutoReset = true };
        _timer.Elapsed += OnElapsed;
    }

    public void Toggle()
    {
        if (_timer.Enabled) Stop();
        else _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        Interlocked.Exchange(ref _busy, 0);
    }

    private void OnElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var desired = _settings.RulloSamplingInterval;
        if (Math.Abs(_timer.Interval - desired) > 1.0)
            _timer.Interval = desired;
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
        try { CaptureTick?.Invoke(this, EventArgs.Empty); }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    public void Dispose() => _timer.Dispose();
}
