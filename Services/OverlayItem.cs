using System.Windows;
using System.Windows.Media;

namespace ScreenTranslator.Services;

public sealed class OverlayItem
{
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public Rect ScreenBounds { get; set; }
    public Color Background { get; set; }
    public Color Foreground { get; set; }
    public bool IsRightToLeft { get; set; }
}
