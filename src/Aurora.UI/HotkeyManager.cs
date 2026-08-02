using System.Runtime.InteropServices;
using System.Windows.Interop;
using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Aurora.UI;

public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int MOD_ALT = 0x0001;
    private const int MOD_CTRL = 0x0002;
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_WIN = 0x0008;
    private const int HotkeyIdTrigger = 1;
    private const int HotkeyIdRullo = 2;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IPublisher _publisher;
    private readonly IAppSettings _settings;
    private readonly ILogger<HotkeyManager> _logger;
    private HwndSource? _source;

    public HotkeyManager(IPublisher publisher, IAppSettings settings, ILogger<HotkeyManager> logger)
    {
        _publisher = publisher;
        _settings = settings;
        _logger = logger;
    }

    public void Register()
    {
        var parameters = new HwndSourceParameters("AuroraHotkeys") { WindowStyle = 0 };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        RegisterKey(HotkeyIdTrigger, _settings.HotkeyTrigger);
        RegisterKey(HotkeyIdRullo, _settings.HotkeyRullo);
    }

    private void RegisterKey(int id, string hotkey)
    {
        var (modifiers, vk) = ParseHotkey(hotkey);
        if (vk == 0)
        {
            _logger.LogWarning("Hotkey '{Hotkey}' contains an unrecognized key — registration skipped.", hotkey);
            return;
        }
        if (!RegisterHotKey(_source!.Handle, id, modifiers, vk))
            _logger.LogWarning("Could not register hotkey '{Hotkey}' — using default combination.", hotkey);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HotkeyIdTrigger) _ = _publisher.Publish(new HotkeyTriggered());
            else if (id == HotkeyIdRullo) _ = _publisher.Publish(new RulloToggleRequested());
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static (int modifiers, int vk) ParseHotkey(string hotkey)
    {
        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries);
        int modifiers = 0;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToUpperInvariant() switch
            {
                "ALT" => MOD_ALT,
                "CTRL" or "CONTROL" => MOD_CTRL,
                "SHIFT" => MOD_SHIFT,
                "WIN" => MOD_WIN,
                _ => 0
            };
        }
        int vk = parts[^1].ToUpperInvariant() switch
        {
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            var k when k.Length == 1 => (int)char.ToUpper(k[0]),
            _ => 0
        };
        return (modifiers, vk);
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            UnregisterHotKey(_source.Handle, HotkeyIdTrigger);
            UnregisterHotKey(_source.Handle, HotkeyIdRullo);
            _source.Dispose();
        }
    }
}
