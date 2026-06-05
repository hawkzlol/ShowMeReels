using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ShowMeReels.App.Services;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x534D52;
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VirtualKeySpace = 0x20;
    private bool _isRegistered;

    public event EventHandler? HotkeyPressed;

    public void Dispose()
    {
        Unregister();
    }

    public void Register(Window window)
    {
        if (_isRegistered)
        {
            return;
        }

        ComponentDispatcher.ThreadFilterMessage += ThreadFilterMessage;

        if (!RegisterHotKey(IntPtr.Zero, HotkeyId, ModControl | ModShift, VirtualKeySpace))
        {
            int win32Error = Marshal.GetLastWin32Error();
            ComponentDispatcher.ThreadFilterMessage -= ThreadFilterMessage;
            throw new InvalidOperationException(
                $"Failed to register Ctrl+Shift+Space as a global hotkey. Win32 error {win32Error}.");
        }

        _isRegistered = true;
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        UnregisterHotKey(IntPtr.Zero, HotkeyId);
        ComponentDispatcher.ThreadFilterMessage -= ThreadFilterMessage;
        _isRegistered = false;
    }

    private void ThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message == WmHotkey && msg.wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
