using System.Runtime.InteropServices;
using ScreenTranslator.Native;

namespace ScreenTranslator.Services.Input;

public enum MouseTriggerKind
{
    Left,
    Middle,
    Right,
    XButton1,
    XButton2,
    WheelUp,
    WheelDown
}

public sealed class MouseTriggeredEventArgs : EventArgs
{
    public MouseTriggerKind Trigger { get; init; }
    public bool Handled { get; set; }
}

public sealed class GlobalMouseHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly Win32.LowLevelKeyboardProc _proc;
    private bool _disposed;

    public event EventHandler<MouseTriggeredEventArgs>? Triggered;

    public GlobalMouseHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
            return;

        var moduleHandle = Win32.GetModuleHandle(null);
        _hookId = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _proc, moduleHandle, 0);

        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException($"SetWindowsHookEx (mouse) failed: {Marshal.GetLastWin32Error()}");
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
        try
        {
            if (nCode < 0)
                return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);

            var message = wParam.ToInt32();
            MouseTriggerKind? trigger = message switch
            {
                Win32.WM_LBUTTONDOWN => MouseTriggerKind.Left,
                Win32.WM_MBUTTONDOWN => MouseTriggerKind.Middle,
                Win32.WM_RBUTTONDOWN => MouseTriggerKind.Right,
                Win32.WM_XBUTTONDOWN => ResolveXButton(lParam),
                Win32.WM_MOUSEWHEEL => ResolveWheel(lParam),
                _ => null
            };

            if (trigger.HasValue)
            {
                var args = new MouseTriggeredEventArgs { Trigger = trigger.Value };
                Triggered?.Invoke(this, args);

                if (args.Handled)
                    return new IntPtr(1);
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Write("GlobalMouseHook.HookCallback", ex);
        }

        return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static MouseTriggerKind ResolveXButton(IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
        var highWord = (int)((data.mouseData >> 16) & 0xFFFF);
        return highWord == Win32.XBUTTON2 ? MouseTriggerKind.XButton2 : MouseTriggerKind.XButton1;
    }

    private static MouseTriggerKind ResolveWheel(IntPtr lParam)
    {
        var data = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
        var delta = unchecked((short)((data.mouseData >> 16) & 0xFFFF));
        return delta > 0 ? MouseTriggerKind.WheelUp : MouseTriggerKind.WheelDown;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~GlobalMouseHook() => Stop();
}
