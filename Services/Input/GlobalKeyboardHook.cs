using System.Runtime.InteropServices;
using System.Windows.Forms;
using ScreenTranslator.Native;

namespace ScreenTranslator.Services.Input;

public sealed class KeyPressedEventArgs : EventArgs
{
    public Keys Key { get; init; }
    public bool Control { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }
    public bool Handled { get; set; }
}

public sealed class GlobalKeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly Win32.LowLevelKeyboardProc _proc;
    private bool _disposed;

    public event EventHandler<KeyPressedEventArgs>? KeyDown;

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
            return;

        var moduleHandle = Win32.GetModuleHandle(null);
        _hookId = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _proc, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException($"SetWindowsHookEx failed: {Marshal.GetLastWin32Error()}");
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
            return;

        Win32.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // This runs on a native callback from Windows. Letting an exception escape
        // here can silently take the hook out, or the whole process down, with no
        // dialog and no clue why - so every path below is guarded.
        try
        {
            if (nCode < 0)
                return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);

            var message = wParam.ToInt32();

            if (message is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);

                var args = new KeyPressedEventArgs
                {
                    Key = (Keys)data.vkCode,
                    Control = (Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0,
                    Alt = (Win32.GetAsyncKeyState(Win32.VK_MENU) & 0x8000) != 0,
                    Shift = (Win32.GetAsyncKeyState(Win32.VK_SHIFT) & 0x8000) != 0
                };

                KeyDown?.Invoke(this, args);

                if (args.Handled)
                    return new IntPtr(1);
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Write("GlobalKeyboardHook.HookCallback", ex);
        }

        return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~GlobalKeyboardHook() => Stop();
}
