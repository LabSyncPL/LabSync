namespace LabSync.Core.Interfaces;

/// <summary>
/// Provides remote input injection (mouse + keyboard) into the local desktop session.
/// </summary>
public interface IInputInjectionService
{
    // ── Mouse ──────────────────────────────────────────────────────────────────

    /// <summary>Move mouse to absolute normalized coordinates [0.0 – 1.0].</summary>
    void MoveMouse(double normalizedX, double normalizedY);

    /// <summary>Press or release a mouse button.</summary>
    void SendMouseButton(MouseButton button, bool down);

    /// <summary>Scroll the mouse wheel. Positive = up, negative = down.</summary>
    void ScrollWheel(int delta);

    // ── Keyboard ───────────────────────────────────────────────────────────────

    /// <summary>Press or release a key identified by its virtual-key code.</summary>
    void SendKey(ushort virtualKeyCode, bool down);

    /// <summary>Type a Unicode character directly (e.g. from a text paste).</summary>
    void SendUnicodeChar(char character, bool down);
}

public enum MouseButton
{
    Left,
    Right,
    Middle
}
