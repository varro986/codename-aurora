using Aurora.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aurora.Admin;

public sealed class AdminService
{
    private readonly IAppSettings _settings;
    private readonly ILogger<AdminService> _logger;

    public AdminService(IAppSettings settings, ILogger<AdminService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Fires with the new version string when an update is found.</summary>
    public event EventHandler<string>? UpdateFound;

    /// <summary>Checks GitHub Releases for updates. Velopack integration pending (US-001).</summary>
    public async Task CheckForUpdatesAsync(CancellationToken ct = default)
    {
        // TODO: integrate Velopack UpdateManager when Velopack NuGet is added
        _logger.LogInformation("Update check on channel '{Channel}' — Velopack integration pending.", _settings.UpdateChannel);
        await Task.CompletedTask;
    }
}
