namespace Aurora.Core.Interfaces;

public interface IContinuousModeController : IDisposable
{
    bool IsActive { get; }
    void Toggle();
    void Stop();
    event EventHandler? CaptureTick;
}
