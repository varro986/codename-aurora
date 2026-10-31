using System.Runtime.InteropServices;
using Aurora.Core.Contracts;
using Aurora.Windows.Hotkey;

namespace Aurora.Windows.Tests.Hotkey;

public sealed class WindowsHotkeyTests
{
    // Unlikely to be used by anything real.
    private static readonly HotkeyGesture Gesture =
        new(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, "F23");

    [Fact]
    public void TryRegister_free_gesture_returns_true()
    {
        using var hotkey = new WindowsHotkey();

        Assert.True(hotkey.TryRegister(Gesture));
    }

    [Fact]
    public void TryRegister_gesture_held_by_another_instance_returns_false()
    {
        using var first = new WindowsHotkey();
        using var second = new WindowsHotkey();
        Assert.True(first.TryRegister(Gesture));

        Assert.False(second.TryRegister(Gesture));
    }

    [Fact]
    public void Dispose_releases_the_gesture()
    {
        var first = new WindowsHotkey();
        using var second = new WindowsHotkey();
        Assert.True(first.TryRegister(Gesture));

        first.Dispose();

        Assert.True(second.TryRegister(Gesture));
    }

    [Fact]
    public void TryRegister_again_replaces_the_previous_registration()
    {
        using var first = new WindowsHotkey();
        using var second = new WindowsHotkey();
        Assert.True(first.TryRegister(Gesture));

        Assert.True(first.TryRegister(Gesture with { Key = "F22" }));
        Assert.True(second.TryRegister(Gesture));
    }

    [Fact]
    public void TryRegister_failing_replacement_keeps_the_previous_registration()
    {
        using var first = new WindowsHotkey();
        using var other = new WindowsHotkey();
        using var probe = new WindowsHotkey();
        var taken = Gesture with { Key = "F22" };
        Assert.True(first.TryRegister(Gesture));
        Assert.True(other.TryRegister(taken));

        Assert.False(first.TryRegister(taken));

        Assert.False(probe.TryRegister(Gesture));
    }

    [Fact]
    public void TryRegister_same_gesture_again_succeeds()
    {
        using var hotkey = new WindowsHotkey();
        Assert.True(hotkey.TryRegister(Gesture));

        Assert.True(hotkey.TryRegister(Gesture));
    }

    [Fact]
    public async Task TryRegister_after_the_loop_ended_throws_instead_of_blocking()
    {
        using var hotkey = new WindowsHotkey();
        Assert.True(PostThreadMessage(hotkey.ThreadId, WmQuit, 0, 0));
        SpinWait.SpinUntil(() => hotkey.HasStopped, TimeSpan.FromSeconds(5));
        Assert.True(hotkey.HasStopped);

        var attempt = Task.Run(() => hotkey.TryRegister(Gesture), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => attempt.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Dispose_after_the_loop_ended_does_not_block()
    {
        var hotkey = new WindowsHotkey();
        Assert.True(PostThreadMessage(hotkey.ThreadId, WmQuit, 0, 0));
        SpinWait.SpinUntil(() => hotkey.HasStopped, TimeSpan.FromSeconds(5));

        var dispose = Task.Run(hotkey.Dispose, TestContext.Current.CancellationToken);

        await dispose.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Concurrent_register_and_dispose_never_block()
    {
        for (var round = 0; round < 20; round++)
        {
            var hotkey = new WindowsHotkey();
            var callers = Enumerable.Range(0, 4)
                .Select(_ => Task.Run(() =>
                {
                    try
                    {
                        hotkey.TryRegister(Gesture);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }, TestContext.Current.CancellationToken))
                .ToArray();

            hotkey.Dispose();

            await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Pressed_is_raised_on_a_thread_pool_thread_and_a_blocked_handler_does_not_stall_the_loop()
    {
        using var hotkey = new WindowsHotkey();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var onPool = false;
        hotkey.Pressed += (_, _) =>
        {
            onPool = Thread.CurrentThread.IsThreadPoolThread;
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        };

        Assert.True(PostThreadMessage(hotkey.ThreadId, WmHotkey, 0, 0));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        // The loop still serves registrations while the handler is blocked.
        var register = Task.Run(() => hotkey.TryRegister(Gesture), TestContext.Current.CancellationToken);
        await register.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        release.Set();
        Assert.True(onPool);
    }

    [Fact]
    public void Dispose_twice_is_harmless()
    {
        var hotkey = new WindowsHotkey();
        hotkey.TryRegister(Gesture);

        hotkey.Dispose();
        hotkey.Dispose();
    }

    [Fact]
    public void TryRegister_after_dispose_throws()
    {
        var hotkey = new WindowsHotkey();
        hotkey.Dispose();

        Assert.Throws<ObjectDisposedException>(() => hotkey.TryRegister(Gesture));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("AB")]
    [InlineData("F0")]
    [InlineData("F25")]
    [InlineData("F1A")]
    [InlineData("Enter")]
    public void TryRegister_invalid_key_throws(string key)
    {
        using var hotkey = new WindowsHotkey();

        Assert.Throws<ArgumentException>(() => hotkey.TryRegister(Gesture with { Key = key }));
    }

    [Theory]
    [InlineData("A", 0x41u)]
    [InlineData("Z", 0x5Au)]
    [InlineData("0", 0x30u)]
    [InlineData("9", 0x39u)]
    [InlineData("F1", 0x70u)]
    [InlineData("F8", 0x77u)]
    [InlineData("F24", 0x87u)]
    public void ToVirtualKey_maps_names_to_win32_codes(string key, uint expected)
    {
        Assert.Equal(expected, WindowsHotkey.ToVirtualKey(key));
    }

    // Injects real keyboard input into the shared desktop: not run by agents in parallel.
    [Fact]
    [Trait("Category", "Desktop")]
    public void Pressed_is_raised_when_the_gesture_is_typed()
    {
        using var hotkey = new WindowsHotkey();
        using var raised = new ManualResetEventSlim();
        hotkey.Pressed += (_, _) => raised.Set();
        Assert.True(hotkey.TryRegister(Gesture));

        SendGesture();

        Assert.True(raised.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    private const uint WmQuit = 0x0012;
    private const uint WmHotkey = 0x0312;

    // Posts to the loop thread's queue only: no keyboard input reaches the desktop.
    private static bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam)
    {
        var address = NativeLibrary.GetExport(NativeLibrary.Load("user32.dll"), "PostThreadMessageW");
        return Marshal.GetDelegateForFunctionPointer<PostThreadMessageFunction>(address)(threadId, message, wParam, lParam);
    }

    private delegate bool PostThreadMessageFunction(uint threadId, uint message, nuint wParam, nint lParam);

    private static void SendGesture()
    {
        const ushort Control = 0x11, Alt = 0x12, Shift = 0x10, F23 = 0x86;
        ushort[] keys = [Control, Alt, Shift, F23];
        var inputs = keys.Select(k => Key(k, up: false))
            .Concat(keys.Reverse().Select(k => Key(k, up: true)))
            .ToArray();

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        Assert.Equal((uint)inputs.Length, sent);
    }

    private static Input Key(ushort virtualKey, bool up) =>
        new() { Type = 1, Vk = virtualKey, Flags = up ? 0x0002u : 0u };

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Vk;
        [FieldOffset(12)] public uint Flags;
    }

    private delegate uint SendInputFunction(uint count, Input[] inputs, int size);

    // Resolved at runtime: the test project does not allow unsafe code, which LibraryImport needs.
    private static uint SendInput(uint count, Input[] inputs, int size)
    {
        var address = NativeLibrary.GetExport(NativeLibrary.Load("user32.dll"), "SendInput");
        return Marshal.GetDelegateForFunctionPointer<SendInputFunction>(address)(count, inputs, size);
    }
}
