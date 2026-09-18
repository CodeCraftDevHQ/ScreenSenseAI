using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenTranslator.Models;
using ScreenTranslator.Native;
using ScreenTranslator.Services;

namespace ScreenTranslator.Views;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _autoHide = new();
    private AppSettings _settings = new();

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        _autoHide.Tick += (_, _) => Clear();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        Win32.MakeClickThrough(handle);
        Win32.HideFromAltTab(handle);
    }

    public void ShowTranslations(IReadOnlyList<OverlayItem> items, AppSettings settings, System.Drawing.Rectangle screenArea)
    {
        _settings = settings;
        RootCanvas.Children.Clear();

        if (items.Count == 0)
        {
            Clear();
            return;
        }

        PlaceWindow(screenArea);

        var scale = GetScale();

        foreach (var item in items)
            RootCanvas.Children.Add(BuildBlock(item, screenArea, scale));

        Opacity = settings.OverlayOpacity;
        Show();

        _autoHide.Stop();

        if (settings.AutoHideOverlay)
        {
            _autoHide.Interval = TimeSpan.FromSeconds(Math.Max(3, settings.OverlayTimeoutSeconds));
            _autoHide.Start();
        }
    }

    private FrameworkElement BuildBlock(OverlayItem item, System.Drawing.Rectangle screenArea, double scale)
    {
        var width = item.ScreenBounds.Width / scale;
        var height = item.ScreenBounds.Height / scale;

        var border = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(item.Background),
            CornerRadius = new CornerRadius(2),
            SnapsToDevicePixels = true
        };

        var text = new TextBlock
        {
            Text = item.TranslatedText,
            Foreground = new SolidColorBrush(item.Foreground),
            FontFamily = new FontFamily(_settings.OverlayFontFamily),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = item.IsRightToLeft ? TextAlignment.Right : TextAlignment.Left,
            FlowDirection = item.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1, 0, 1, 0),
            FontSize = FitFontSize(item.TranslatedText, width, height)
        };

        border.Child = text;

        if (_settings.ShowOriginalOnHover)
            border.ToolTip = item.OriginalText;

        Canvas.SetLeft(border, (item.ScreenBounds.X - screenArea.X) / scale);
        Canvas.SetTop(border, (item.ScreenBounds.Y - screenArea.Y) / scale);

        return border;
    }

    private double FitFontSize(string text, double width, double height)
    {
        var maximum = Math.Min(_settings.MaxFontSize, height * 0.95);
        var minimum = 7.0;

        if (string.IsNullOrWhiteSpace(text) || width <= 0 || height <= 0)
            return Math.Max(minimum, Math.Min(maximum, height * 0.7));

        var typeface = new Typeface(_settings.OverlayFontFamily);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (var size = maximum; size >= minimum; size -= 0.5)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                size,
                Brushes.Black,
                pixelsPerDip)
            {
                MaxTextWidth = width
            };

            if (formatted.Height <= height)
                return size;
        }

        return minimum;
    }

    private double GetScale()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
    }

    private void PlaceWindow(System.Drawing.Rectangle area)
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();

        Win32Window.SetWindowPos(
            handle,
            Win32Window.HWND_TOPMOST,
            area.X,
            area.Y,
            area.Width,
            area.Height,
            Win32Window.SWP_NOACTIVATE | Win32Window.SWP_SHOWWINDOW);
    }

    public void Clear()
    {
        _autoHide.Stop();
        RootCanvas.Children.Clear();
        Hide();
    }
}
