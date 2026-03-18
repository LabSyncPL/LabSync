using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LabSync.Core.Interfaces;

namespace LabSync.Agent.Services;

/// <summary>
/// Windows implementation of <see cref="IInputInjectionService"/> backed by
/// <c>SendInput</c> from <c>user32.dll</c>.
///
/// Register in DI as a singleton:
///   builder.Services.AddSingleton&lt;IInputInjectionService, WindowsInputInjectionService&gt;();
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsInputInjectionService : IInputInjectionService
{
    // ══════════════════════════════════════════════════════════════════════════
    //  P/Invoke declarations
    // ══════════════════════════════════════════════════════════════════════════

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    // ── Structures ────────────────────────────────────────────────────────────

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

    // ── INPUT type constants ──────────────────────────────────────────────────
    private const uint INPUT_MOUSE    = 0;
    private const uint INPUT_KEYBOARD = 1;

    // ── MOUSEEVENTF flags ─────────────────────────────────────────────────────
    private const uint MOUSEEVENTF_MOVE        = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN    = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP      = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN   = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP     = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN  = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP    = 0x0040;
    private const uint MOUSEEVENTF_WHEEL       = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE    = 0x8000;

    // ── KEYEVENTF flags ───────────────────────────────────────────────────────
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP       = 0x0002;
    private const uint KEYEVENTF_UNICODE     = 0x0004;
    private const uint KEYEVENTF_SCANCODE    = 0x0008;

    // WHEEL_DELTA
    private const int WHEEL_DELTA = 120;

    // ══════════════════════════════════════════════════════════════════════════
    //  IInputInjectionService
    // ══════════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public void MoveMouse(double normalizedX, double normalizedY)
    {
        // SendInput with MOUSEEVENTF_ABSOLUTE expects coordinates in the range
        // [0, 65535] regardless of the actual screen resolution.
        int absX = (int)(Math.Clamp(normalizedX, 0.0, 1.0) * 65535);
        int absY = (int)(Math.Clamp(normalizedY, 0.0, 1.0) * 65535);

        Send(MouseInput(absX, absY, 0, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE));
    }

    /// <inheritdoc/>
    public void SendMouseButton(MouseButton button, bool down)
    {
        uint flags = button switch
        {
            MouseButton.Left   => down ? MOUSEEVENTF_LEFTDOWN   : MOUSEEVENTF_LEFTUP,
            MouseButton.Right  => down ? MOUSEEVENTF_RIGHTDOWN  : MOUSEEVENTF_RIGHTUP,
            MouseButton.Middle => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _                  => throw new ArgumentOutOfRangeException(nameof(button))
        };

        Send(MouseInput(0, 0, 0, flags));
    }

    /// <inheritdoc/>
    public void ScrollWheel(int delta)
    {
        // mouseData = number of WHEEL_DELTAs to scroll
        uint mouseData = (uint)(delta * WHEEL_DELTA);
        Send(MouseInput(0, 0, mouseData, MOUSEEVENTF_WHEEL));
    }

    /// <inheritdoc/>
    public void SendKey(ushort virtualKeyCode, bool down)
    {
        uint flags = down ? 0u : KEYEVENTF_KEYUP;

        // Some extended keys (arrows, ins, del, home, end, pgup, pgdn, etc.)
        // need the EXTENDEDKEY flag – set it automatically for the common ones.
        if (IsExtendedKey(virtualKeyCode))
            flags |= KEYEVENTF_EXTENDEDKEY;

        Send(KeyInput(virtualKeyCode, 0, flags));
    }

    /// <inheritdoc/>
    public void SendUnicodeChar(char character, bool down)
    {
        uint flags = KEYEVENTF_UNICODE;
        if (!down) flags |= KEYEVENTF_KEYUP;

        Send(KeyInput(0, character, flags));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private static void Send(INPUT input)
    {
        var inputs = new[] { input };
        uint sent = SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"SendInput failed (Win32 error {err}).");
        }
    }

    private static INPUT MouseInput(int dx, int dy, uint mouseData, uint flags) =>
        new INPUT
        {
            type = INPUT_MOUSE,
            data = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx        = dx,
                    dy        = dy,
                    mouseData = mouseData,
                    dwFlags   = flags
                }
            }
        };

    private static INPUT KeyInput(ushort vk, ushort scan, uint flags) =>
        new INPUT
        {
            type = INPUT_KEYBOARD,
            data = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk   = vk,
                    wScan = scan,
                    dwFlags = flags
                }
            }
        };

    /// <summary>
    /// Returns true for virtual keys that require the EXTENDEDKEY flag.
    /// https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    /// </summary>
    private static bool IsExtendedKey(ushort vk) => vk is
        0x21 or  // VK_PRIOR  (Page Up)
        0x22 or  // VK_NEXT   (Page Down)
        0x23 or  // VK_END
        0x24 or  // VK_HOME
        0x25 or  // VK_LEFT
        0x26 or  // VK_UP
        0x27 or  // VK_RIGHT
        0x28 or  // VK_DOWN
        0x2D or  // VK_INSERT
        0x2E or  // VK_DELETE
        0x5B or  // VK_LWIN
        0x5C or  // VK_RWIN
        0x6F or  // VK_DIVIDE (numpad /)
        0xA1 or  // VK_RSHIFT
        0xA3 or  // VK_RCONTROL
        0xA5;    // VK_RMENU  (Right Alt)
}
