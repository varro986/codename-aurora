using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Aurora.Core.Contracts;
using Aurora.Windows.Interop;

namespace Aurora.Windows.Hotkey;

/// <summary>
/// Global hotkey on a dedicated thread that owns a message-only window and the message loop, because
/// RegisterHotKey binds the hotkey to the thread that calls it. <see cref="Pressed"/> is raised on a thread-pool
/// thread, so a handler can never block or kill the message loop; an exception it throws is unhandled, as for any event.
/// </summary>
public sealed class WindowsHotkey : IHotkey
{
    private const int FirstId = 1;
    private const int SecondId = 2;
    private const int FirstFunctionKey = 0x70;
    private const int LastFunctionNumber = 24;

    private readonly MessageLoopThread _loop;
    private nint _window;
    private Registration? _current;
    private int _disposed;

    public WindowsHotkey() =>
        _loop = new MessageLoopThread("Aurora hotkey", CreateWindow, OnMessage, () => NativeMethods.DestroyWindow(_window));

    public event EventHandler? Pressed;

    internal uint ThreadId => _loop.ThreadId;

    internal bool HasStopped => _loop.HasStopped;

    public bool TryRegister(HotkeyGesture gesture)
    {
        var virtualKey = ToVirtualKey(gesture.Key);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var modifiers = (uint)gesture.Modifiers | NativeMethods.MOD_NOREPEAT;
        return _loop.Invoke(() => RegisterOnThread(modifiers, virtualKey));
    }

    // Destroying the window as the loop ends releases the registered hotkey.
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _loop.Dispose();
        }
    }

    /// <summary>A-Z, 0-9 and F1-F24 map to their Win32 virtual-key codes.</summary>
    internal static uint ToVirtualKey(string key)
    {
        if (key.Length == 1 && (key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9'))
        {
            return key[0];
        }

        if (key.Length is 2 or 3 && key[0] == 'F' && key[1] != '0' && key.Skip(1).All(char.IsAsciiDigit)
            && int.TryParse(key.AsSpan(1), CultureInfo.InvariantCulture, out var number)
            && number is >= 1 and <= LastFunctionNumber)
        {
            return (uint)(FirstFunctionKey + number - 1);
        }

        throw new ArgumentException($"Unknown key name '{key}'.", nameof(key));
    }

    // The new gesture is registered under the other id first, so a failure leaves the previous one in place.
    private bool RegisterOnThread(uint modifiers, uint virtualKey)
    {
        if (_current is { } current && current.Modifiers == modifiers && current.VirtualKey == virtualKey)
        {
            return true;
        }

        var id = _current?.Id == FirstId ? SecondId : FirstId;
        if (!NativeMethods.RegisterHotKey(_window, id, modifiers, virtualKey))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED)
            {
                return false;
            }

            throw new Win32Exception(error);
        }

        Unregister();
        _current = new Registration(id, modifiers, virtualKey);
        return true;
    }

    private void Unregister()
    {
        if (_current is { } current)
        {
            NativeMethods.UnregisterHotKey(_window, current.Id);
            _current = null;
        }
    }

    private void CreateWindow()
    {
        _window = NativeMethods.CreateWindowEx(
            0, "STATIC", null, 0, 0, 0, 0, 0, NativeMethods.HWND_MESSAGE, 0, 0, 0);
        if (_window == 0)
        {
            throw new Win32Exception();
        }
    }

    // WM_HOTKEY is read before dispatch: the STATIC window needs no window procedure of ours.
    private void OnMessage(Interop.NativeMethods.Msg msg)
    {
        if (msg.Message == NativeMethods.WM_HOTKEY && Pressed is { } handler)
        {
            ThreadPool.QueueUserWorkItem(_ => handler(this, EventArgs.Empty));
        }
    }

    private sealed record Registration(int Id, uint Modifiers, uint VirtualKey);
}
