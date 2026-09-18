using System.Windows;

namespace ScreenTranslator.Models;

public sealed class OcrBlock
{
    public string Text { get; set; } = string.Empty;
    public Rect Bounds { get; set; }
    public double Confidence { get; set; }
    public string? TranslatedText { get; set; }
}
