using Aurora.Core.Contracts;
using Aurora.Core.Reading;
using Aurora.Core.Settings;
using Aurora.Overlay;
using Aurora.Windows.Capture;
using Aurora.Windows.Hotkey;
using Aurora.Windows.Ocr;
using Aurora.Windows.Overlay;
using Aurora.Windows.Targets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Aurora.App;

/// <summary>
/// The composition root and the tray: wires the adapters to the attachment and is the only user interface.
/// Every handler runs on the UI thread; events from other threads are posted to it.
/// </summary>
internal sealed class TrayShell : IAsyncDisposable
{
    private readonly AuroraSettings _settings;
    private readonly FileLog _log;
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly WindowsTargetResolver _resolver;
    private readonly WindowsWindowEvents _windowEvents;
    private readonly WindowsHotkey _hotkey;
    private readonly PickLayer _pick = new();
    private readonly Attachment _attachment;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _status = new() { IsEnabled = false };
    private readonly NativeMenuItem _attach = new("Attach") { Menu = [] };
    private readonly NativeMenuItem _detach = new("Detach") { IsEnabled = false };
    private string? _attachedTitle;
    private int _quitting;

    public TrayShell(AuroraSettings settings, FileLog log, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        (_settings, _log, _lifetime) = (settings, log, lifetime);
        _resolver = new WindowsTargetResolver(settings.ProcessModifier, settings.MonitorModifier);
        _windowEvents = new WindowsWindowEvents();
        _hotkey = new WindowsHotkey();
        _attachment = new Attachment(
            new WindowsFrameSourceFactory(settings.ReadingInterval),
            new WindowsOcrEngine(settings.OcrLanguage),
            new Overlay.Overlay(new OverlayWindowStyler()),
            _windowEvents,
            settings,
            TimeProvider.System);
        _attachment.Detached += (_, end) => Dispatcher.UIThread.Post(() => OnDetached(end));
        _hotkey.Pressed += (_, _) => Dispatcher.UIThread.Post(() => Fire(PickAsync, "Cannot pick a target"));

        var pick = new NativeMenuItem($"Pick on screen ({Describe(settings.Hotkey)})");
        pick.Click += (_, _) => Fire(PickAsync, "Cannot pick a target");
        _attach.Menu!.Opening += (_, _) => FillAttachMenu();
        _detach.Click += (_, _) => Fire(DetachAsync, "Cannot detach");
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => Fire(QuitAsync, "Cannot quit cleanly");

        _tray = new TrayIcon
        {
            Icon = TrayIconImage.Create(),
            Menu = [_status, new NativeMenuItemSeparator(), pick, _attach, _detach, new NativeMenuItemSeparator(), quit],
        };
        TrayIcon.SetIcons(Application.Current!, [_tray]);

        // AC-1.7: a taken hotkey is reported, and the tray still offers everything.
        if (_hotkey.TryRegister(settings.Hotkey))
        {
            SetStatus($"Ready: press {Describe(settings.Hotkey)} and click a window.");
        }
        else
        {
            SetStatus($"{Describe(settings.Hotkey)} is used by another application: use Pick or Attach in this menu.", error: true);
        }
    }

    private async Task PickAsync()
    {
        var picked = await _pick.PickAsync();
        if (picked is null)
        {
            return;
        }

        var target = _resolver.Resolve(picked.Point, picked.Modifiers);
        if (target is null)
        {
            SetStatus("Nothing to attach there.");
            return;
        }

        var title = _resolver.Describe(target);
        await AttachAsync(target, title);
    }

    private void FillAttachMenu()
    {
        var menu = _attach.Menu!;
        menu.Items.Clear();
        foreach (var choice in _resolver.List())
        {
            var item = new NativeMenuItem(choice.Title);
            item.Click += (_, _) => Fire(() => AttachAsync(choice.Target, choice.Title), $"Cannot attach to {choice.Title}");
            menu.Items.Add(item);
        }
    }

    // The title is set first: a target that closes at once is reported by name.
    private async Task AttachAsync(Target target, string title)
    {
        _attachedTitle = title;
        try
        {
            await _attachment.AttachAsync(target);
        }
        catch
        {
            if (_attachment.Target is null)
            {
                _attachedTitle = null;
            }

            throw;
        }

        // Another attach, or the end of this one, may have come in the meantime: only the current target is reported.
        if (_attachment.Target == target)
        {
            _detach.IsEnabled = true;
            SetStatus($"Attached to {title}.");
        }
    }

    // Every action started from a menu or the hotkey has no caller to catch for it: its error is logged and shown.
    private void Fire(Func<Task> action, string failure) => _ = RunAsync(action, failure);

    private async Task RunAsync(Func<Task> action, string failure)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus($"{failure}: see {_log.Path}.", error: true, ex);
        }
    }

    private async Task DetachAsync()
    {
        await _attachment.DetachAsync();
        Detached($"Detached from {_attachedTitle}.");
    }

    private void OnDetached(ReadingEnd end)
    {
        var message = end.Kind switch
        {
            ReadingEndKind.TargetClosed => $"{_attachedTitle} closed: detached.",
            ReadingEndKind.OcrLanguageMissing => end.Error!.Message,
            _ => $"Reading stopped after an error: see {_log.Path}.",
        };
        Detached(message, end.Kind != ReadingEndKind.TargetClosed, end.Error);
    }

    private void Detached(string message, bool error = false, Exception? exception = null)
    {
        _attachedTitle = null;
        _detach.IsEnabled = false;
        SetStatus(message, error, exception);
    }

    public async ValueTask DisposeAsync()
    {
        await _attachment.DisposeAsync();
        _hotkey.Dispose();
        _windowEvents.Dispose();
        _tray.Dispose();
    }

    private async Task QuitAsync()
    {
        if (Interlocked.Exchange(ref _quitting, 1) != 0)
        {
            return;
        }

        try
        {
            _log.Info("Quit.");
            await DisposeAsync();
        }
        finally
        {
            _lifetime.Shutdown();
        }
    }

    private void SetStatus(string message, bool error = false, Exception? exception = null)
    {
        _status.Header = message;
        _tray.ToolTipText = $"Aurora: {message}";
        if (error)
        {
            _log.Error(message, exception);
        }
        else
        {
            _log.Info(message);
        }
    }

    private static readonly (KeyModifiers Modifier, string Name)[] ModifierNames =
        [(KeyModifiers.Control, "Ctrl"), (KeyModifiers.Alt, "Alt"), (KeyModifiers.Shift, "Shift"), (KeyModifiers.Windows, "Win")];

    private static string Describe(HotkeyGesture gesture) =>
        string.Join('+', ModifierNames.Where(m => gesture.Modifiers.HasFlag(m.Modifier)).Select(m => m.Name).Append(gesture.Key));
}
