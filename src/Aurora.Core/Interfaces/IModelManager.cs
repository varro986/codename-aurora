namespace Aurora.Core.Interfaces;

public interface IModelManager : IAsyncDisposable
{
    bool IsLoaded { get; }
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
}
