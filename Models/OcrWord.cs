using System.Windows;

namespace ScreenTranslator.Models;

public sealed class OcrWord
{
    public string Text { get; set; } = string.Empty;
    public Rect Bounds { get; set; }
}
