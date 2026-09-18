using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenTranslator.Views;

public partial class RegionSelectWindow : Window
{
    private Point _origin;
    private bool _dragging;

    public System.Drawing.Rectangle? SelectedRegion { get; private set; }

    public RegionSelectWindow()
    {
        InitializeComponent();

        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        Left = virtualScreen.Left;
        Top = virtualScreen.Top;
        Width = virtualScreen.Width;
        Height = virtualScreen.Height;

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        SelectedRegion = null;
        DialogResult = false;
        Close();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _origin = e.GetPosition(RootCanvas);
        _dragging = true;

        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _origin.X);
        Canvas.SetTop(SelectionRect, _origin.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        var current = e.GetPosition(RootCanvas);

        Canvas.SetLeft(SelectionRect, Math.Min(_origin.X, current.X));
        Canvas.SetTop(SelectionRect, Math.Min(_origin.Y, current.Y));
        SelectionRect.Width = Math.Abs(current.X - _origin.X);
        SelectionRect.Height = Math.Abs(current.Y - _origin.Y);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;

        var scale = VisualTreeHelperDpi();
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

        var x = (int)(Canvas.GetLeft(SelectionRect) * scale) + virtualScreen.Left;
        var y = (int)(Canvas.GetTop(SelectionRect) * scale) + virtualScreen.Top;
        var w = (int)(SelectionRect.Width * scale);
        var h = (int)(SelectionRect.Height * scale);

        SelectedRegion = w > 10 && h > 10 ? new System.Drawing.Rectangle(x, y, w, h) : null;
        DialogResult = SelectedRegion is not null;
        Close();
    }

    private double VisualTreeHelperDpi()
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        return dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
    }
}
