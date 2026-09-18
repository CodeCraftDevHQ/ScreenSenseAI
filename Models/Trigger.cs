namespace ScreenTranslator.Models;

public enum TriggerKind
{
    Key,
    MouseButton,
    WheelUp,
    WheelDown
}

public enum TriggerMouseButton
{
    Left,
    Middle,
    Right,
    XButton1,
    XButton2
}

public sealed class Trigger
{
    public TriggerKind Kind { get; set; } = TriggerKind.Key;
    public System.Windows.Forms.Keys Key { get; set; } = System.Windows.Forms.Keys.None;
    public TriggerMouseButton MouseButton { get; set; } = TriggerMouseButton.Left;

    public static Trigger FromKey(System.Windows.Forms.Keys key) => new() { Kind = TriggerKind.Key, Key = key };

    public override string ToString() => Kind switch
    {
        TriggerKind.Key => Key.ToString(),
        TriggerKind.MouseButton => $"Mouse {MouseButton}",
        TriggerKind.WheelUp => "Wheel Up",
        TriggerKind.WheelDown => "Wheel Down",
        _ => "None"
    };
}
