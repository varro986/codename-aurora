using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.UI;

public sealed class TrayIconManager : IDisposable
{
    private readonly IPublisher _publisher;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private readonly System.Drawing.Icon _idleIcon = System.Drawing.SystemIcons.Application;
    private readonly System.Drawing.Icon _activeIcon = System.Drawing.SystemIcons.Information;

    public TrayIconManager(IPublisher publisher) => _publisher = publisher;

    public void Initialize()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Aurora",
            Icon = _idleIcon,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
    }

    public void ShowUpdateNotification(string version) =>
        _notifyIcon?.ShowBalloonTip(5000, "Aurora Update", $"Version {version} available.", System.Windows.Forms.ToolTipIcon.Info);

    public void SetContinuousActive(bool isActive)
    {
        if (_notifyIcon is null) return;
        _notifyIcon.Text = isActive ? "Aurora — Rullo active" : "Aurora";
        _notifyIcon.Icon = isActive ? _activeIcon : _idleIcon;
    }

    public void ShowDictionaryReloadedNotification() =>
        _notifyIcon?.ShowBalloonTip(2000, "Aurora", "Dictionary reloaded.", System.Windows.Forms.ToolTipIcon.None);

    private System.Windows.Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => _ = _publisher.Publish(new OpenSettingsRequested()));
        menu.Items.Add("About Aurora", null, (_, _) =>
            System.Windows.Forms.MessageBox.Show(
                "Aurora\nAutomated HMI translation tool.",
                "About Aurora",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information));
        menu.Items.Add("Exit", null, (_, _) => _ = _publisher.Publish(new ShutdownRequested()));
        return menu;
    }

    public void Dispose() => _notifyIcon?.Dispose();
}
