using System.Windows.Forms;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Input;

public sealed class HotkeyManager : IDisposable
{
    private readonly GlobalKeyboardHook _keyboardHook = new();
    private readonly GlobalMouseHook _mouseHook = new();
    private readonly SettingsService _settings;
    private bool _suspended;

    public event EventHandler? TranslateRequested;
    public event EventHandler? ClearRequested;
    public event EventHandler? RegionSelectRequested;
    public event EventHandler? TranslateSavedRegionRequested;
    public event EventHandler? AdHocRegionRequested;

    public HotkeyManager(SettingsService settings)
    {
        _settings = settings;
        _keyboardHook.KeyDown += OnKeyDown;
        _mouseHook.Triggered += OnMouseTriggered;
    }

    public void Start()
    {
        _keyboardHook.Start();
        _mouseHook.Start();
    }

    public void Suspend() => _suspended = true;

    public void Resume() => _suspended = false;

    private void OnKeyDown(object? sender, KeyPressedEventArgs e)
    {
        if (_suspended)
            return;

        var s = _settings.Current;

        if (s.HotkeyRequiresControl && !e.Control) return;
        if (s.HotkeyRequiresAlt && !e.Alt) return;
        if (s.HotkeyRequiresShift && !e.Shift) return;

        if (Matches(s.TranslateTrigger, e.Key))
        {
            e.Handled = s.SwallowHotkey;
            TranslateRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (Matches(s.ClearOverlayTrigger, e.Key))
        {
            ClearRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (Matches(s.RegionSelectTrigger, e.Key))
        {
            e.Handled = s.SwallowHotkey;
            RegionSelectRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (Matches(s.TranslateSavedRegionTrigger, e.Key))
        {
            e.Handled = s.SwallowHotkey;
            TranslateSavedRegionRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (Matches(s.AdHocRegionTrigger, e.Key))
        {
            e.Handled = s.SwallowHotkey;
            AdHocRegionRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMouseTriggered(object? sender, MouseTriggeredEventArgs e)
    {
        if (_suspended)
            return;

        var s = _settings.Current;

        if (Matches(s.TranslateTrigger, e.Trigger))
            TranslateRequested?.Invoke(this, EventArgs.Empty);
        else if (Matches(s.ClearOverlayTrigger, e.Trigger))
            ClearRequested?.Invoke(this, EventArgs.Empty);
        else if (Matches(s.RegionSelectTrigger, e.Trigger))
            RegionSelectRequested?.Invoke(this, EventArgs.Empty);
        else if (Matches(s.TranslateSavedRegionTrigger, e.Trigger))
            TranslateSavedRegionRequested?.Invoke(this, EventArgs.Empty);
        else if (Matches(s.AdHocRegionTrigger, e.Trigger))
            AdHocRegionRequested?.Invoke(this, EventArgs.Empty);
    }

    private static bool Matches(HotkeyTrigger trigger, Keys key)
        => trigger.Kind == TriggerKind.Key && trigger.Key == key;

    private static bool Matches(HotkeyTrigger trigger, MouseTriggerKind mouseTrigger) => mouseTrigger switch
    {
        MouseTriggerKind.WheelUp => trigger.Kind == TriggerKind.WheelUp,
        MouseTriggerKind.WheelDown => trigger.Kind == TriggerKind.WheelDown,
        MouseTriggerKind.Left => trigger.Kind == TriggerKind.MouseButton && trigger.MouseButton == TriggerMouseButton.Left,
        MouseTriggerKind.Middle => trigger.Kind == TriggerKind.MouseButton && trigger.MouseButton == TriggerMouseButton.Middle,
        MouseTriggerKind.Right => trigger.Kind == TriggerKind.MouseButton && trigger.MouseButton == TriggerMouseButton.Right,
        MouseTriggerKind.XButton1 => trigger.Kind == TriggerKind.MouseButton && trigger.MouseButton == TriggerMouseButton.XButton1,
        MouseTriggerKind.XButton2 => trigger.Kind == TriggerKind.MouseButton && trigger.MouseButton == TriggerMouseButton.XButton2,
        _ => false
    };

    public void Dispose()
    {
        _keyboardHook.Dispose();
        _mouseHook.Dispose();
    }
}
