using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace ShowMeReels.App.Services;

public sealed class GlobalArrowCaptureService : IGlobalArrowCaptureService
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkUp = 0x26;
    private const int VkDown = 0x28;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private readonly LowLevelKeyboardProc _hookCallback;
    private IntPtr _hookHandle;

    public GlobalArrowCaptureService()
    {
        _hookCallback = HookCallback;
    }

    public event EventHandler<GlobalArrowPressedEventArgs>? ArrowPressed;

    public bool IsEnabled { get; private set; }

    public void Dispose()
    {
        SetEnabled(enabled: false);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == IsEnabled)
        {
            return;
        }

        if (enabled)
        {
            InstallHook();
        }
        else
        {
            RemoveHook();
        }

        IsEnabled = enabled;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || !IsEnabled)
        {
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        int message = wParam.ToInt32();
        if (message is not WmKeyDown and not WmSysKeyDown)
        {
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        KbdLlHookStruct keyboardData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        int direction = keyboardData.VirtualKeyCode switch
        {
            VkUp => -1,
            VkDown => 1,
            _ => 0,
        };

        if (direction == 0 || AreModifierKeysPressed())
        {
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            ArrowPressed?.Invoke(this, new GlobalArrowPressedEventArgs(direction))));

        return new IntPtr(1);
    }

    private void InstallHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using Process currentProcess = Process.GetCurrentProcess();
        using ProcessModule? currentModule = currentProcess.MainModule;
        IntPtr moduleHandle = GetModuleHandle(currentModule?.ModuleName);

        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookCallback, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            int win32Error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to install the global Up/Down arrow capture hook. Win32 error {win32Error}.");
        }
    }

    private void RemoveHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private static bool AreModifierKeysPressed()
    {
        return IsKeyPressed(VkShift)
            || IsKeyPressed(VkControl)
            || IsKeyPressed(VkMenu)
            || IsKeyPressed(VkLWin)
            || IsKeyPressed(VkRWin);
    }

    private static bool IsKeyPressed(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KbdLlHookStruct
    {
        public int VirtualKeyCode { get; init; }
        public int ScanCode { get; init; }
        public int Flags { get; init; }
        public int Time { get; init; }
        public IntPtr ExtraInfo { get; init; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
