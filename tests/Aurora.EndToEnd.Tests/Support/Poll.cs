namespace Aurora.EndToEnd.Tests.Support;

internal static class Poll
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Checks the condition every 50 ms until it holds; fails with the message after the timeout.</summary>
    public static async Task UntilAsync(Func<bool> condition, string failure, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        if (!await HoldsAsync(condition, cancellationToken, timeout))
        {
            throw new TimeoutException(failure);
        }
    }

    /// <summary>True when the condition held before the timeout.</summary>
    public static async Task<bool> HoldsAsync(Func<bool> condition, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(50, cancellationToken);
        }

        return true;
    }
}
