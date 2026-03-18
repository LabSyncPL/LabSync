using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LabSync.Modules.RemoteDesktop.Abstractions;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.RemoteDesktop.Infrastructure;

[SupportedOSPlatform("windows")]
internal sealed class WindowsInputInjectionService : IInputInjectionService
{
    private readonly ILogger? _logger;

    public WindowsInputInjectionService(ILogger? logger) => _logger = logger;

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int    dx;
        public int    dy;
        public uint   mouseData;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_MOUSE    = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE       = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP     = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;
    private const uint MOUSEEVENTF_WHEEL      = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE   = 0x8000;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP       = 0x0002;

    private const int WHEEL_DELTA = 120;

    // ── IInputInjectionService ────────────────────────────────────────────────

    /// <summary>
    /// x, y are absolute pixel coordinates scaled to the remote desktop resolution
    /// (frontend sends values mapped to videoWidth / videoHeight).
    /// We convert them to [0–65535] required by MOUSEEVENTF_ABSOLUTE.
    /// </summary>
    public Task InjectMouseMoveAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);
        if (screenW == 0 || screenH == 0) return Task.CompletedTask;

        int absX = (int)((double)x / screenW * 65535);
        int absY = (int)((double)y / screenH * 65535);

        Send(MouseInput(absX, absY, 0, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE));
        return Task.CompletedTask;
    }

    public Task InjectMouseButtonAsync(MouseButton button, bool pressed, CancellationToken cancellationToken = default)
    {
        uint flags = button switch
        {
            MouseButton.Left   => pressed ? MOUSEEVENTF_LEFTDOWN   : MOUSEEVENTF_LEFTUP,
            MouseButton.Right  => pressed ? MOUSEEVENTF_RIGHTDOWN  : MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => pressed ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _                  => throw new ArgumentOutOfRangeException(nameof(button))
        };

        Send(MouseInput(0, 0, 0, flags));
        return Task.CompletedTask;
    }

    public Task InjectMouseWheelAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default)
    {
        if (deltaY != 0)
        {
            uint mouseData = (uint)(deltaY * WHEEL_DELTA);
            Send(MouseInput(0, 0, mouseData, MOUSEEVENTF_WHEEL));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// keyCode = browser KeyboardEvent.keyCode – maps 1:1 to Windows VK codes
    /// for the vast majority of keys.
    /// </summary>
    public Task InjectKeyAsync(int keyCode, bool pressed, CancellationToken cancellationToken = default)
    {
        var vk = (ushort)keyCode;
        uint flags = pressed ? 0u : KEYEVENTF_KEYUP;
        if (IsExtendedKey(vk)) flags |= KEYEVENTF_EXTENDEDKEY;

        Send(KeyInput(vk, 0, flags));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Send(INPUT input)
    {
        uint sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        if (sent == 0)
            _logger?.LogWarning("SendInput failed (Win32 error {Error}).", Marshal.GetLastWin32Error());
    }

    private static INPUT MouseInput(int dx, int dy, uint mouseData, uint flags) => new()
    {
        type = INPUT_MOUSE,
        data = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = mouseData, dwFlags = flags } }
    };

    private static INPUT KeyInput(ushort vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        data = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags } }
    };

    private static bool IsExtendedKey(ushort vk) => vk is
        0x21 or 0x22 or 0x23 or 0x24 or  // PgUp, PgDn, End, Home
        0x25 or 0x26 or 0x27 or 0x28 or  // Left, Up, Right, Down
        0x2D or 0x2E or                   // Insert, Delete
        0x5B or 0x5C or                   // LWin, RWin
        0x6F or 0xA1 or 0xA3 or 0xA5;    // Num/, RShift, RCtrl, RAlt
}
