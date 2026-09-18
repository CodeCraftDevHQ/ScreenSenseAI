using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using ScreenTranslator.Localization;
using ScreenTranslator.Native;
using ScreenTranslator.Services;

namespace ScreenTranslator.Views;

public partial class FloatingIconWindow : Window
{
    private readonly SettingsService _settings;
    private bool _isOpen;
    private Point _dragStart;
    private bool _isDragging;

    public event EventHandler? TranslateRequested;
    public event EventHandler? RegionRequested;
    public event EventHandler? SavedRegionRequested;
    public event EventHandler? AdHocRegionRequested;
    public event EventHandler? ClearRequested;
    public event EventHandler? TextViewRequested;
    public event EventHandler? ManualTranslateRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public FloatingIconWindow(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
        => Win32.HideFromAltTab(new WindowInteropHelper(this).Handle);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Opacity = _settings.Current.FloatingIconOpacity;
        Reposition();
    }

    public void Reposition()
    {
        var work = SystemParameters.WorkArea;
        Top = Math.Min(Math.Max(0, _settings.Current.FloatingIconTop), work.Height - ActualHeight);
        if (_settings.Current.FloatingIconOnRightEdge)
        {
            Left = work.Right - ActualWidth - 4;
        }
        else
        {
            Left = Math.Min(Math.Max(work.Left + 0, _settings.Current.FloatingIconLeft), work.Right - ActualWidth - 0);
        }
    }

    public void SetStatus(string key) => StatusText.Text = Loc.Instance[key];

    private void OnIconMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = false;

        IconRoot.CaptureMouse();
        IconRoot.MouseMove += OnIconMouseMove;
        IconRoot.MouseLeftButtonUp += OnIconMouseUp;
    }

    private void OnIconMouseMove(object sender, MouseEventArgs e)
    {
        var current = e.GetPosition(this);

        var deltaX = current.X - _dragStart.X;
        var deltaY = current.Y - _dragStart.Y;

        if (Math.Abs(deltaX) > 4 || Math.Abs(deltaY) > 4)
        {
            _isDragging = true;
            Top += deltaY;
            Left += deltaX;
        }
    }

    private void OnIconMouseUp(object sender, MouseButtonEventArgs e)
    {
        IconRoot.ReleaseMouseCapture();
        IconRoot.MouseMove -= OnIconMouseMove;
        IconRoot.MouseLeftButtonUp -= OnIconMouseUp;

        if (_isDragging)
        {
            _settings.Current.FloatingIconTop = Top;
            _settings.Current.FloatingIconLeft = Left;
            _settings.Current.FloatingIconOnRightEdge = false;
            _settings.Save();
            return;
        }

        TogglePanel();
    }

    private void OnIconRightClick(object sender, MouseButtonEventArgs e) => TranslateRequested?.Invoke(this, EventArgs.Empty);

    public void TogglePanel()
    {
        _isOpen = !_isOpen;

        var animation = new DoubleAnimation
        {
            To = _isOpen ? PanelContent.Width : 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        PanelRoot.BeginAnimation(WidthProperty, animation);
    }

    public void ClosePanel()
    {
        if (!_isOpen)
            return;

        TogglePanel();
    }

    private void OnTranslateClick(object sender, RoutedEventArgs e) => TranslateRequested?.Invoke(this, EventArgs.Empty);

    private void OnRegionClick(object sender, RoutedEventArgs e) => RegionRequested?.Invoke(this, EventArgs.Empty);

    private void OnSavedRegionClick(object sender, RoutedEventArgs e) => SavedRegionRequested?.Invoke(this, EventArgs.Empty);

    private void OnAdHocRegionClick(object sender, RoutedEventArgs e) => AdHocRegionRequested?.Invoke(this, EventArgs.Empty);

    private void OnClearClick(object sender, RoutedEventArgs e) => ClearRequested?.Invoke(this, EventArgs.Empty);

    private void OnTextViewClick(object sender, RoutedEventArgs e) => TextViewRequested?.Invoke(this, EventArgs.Empty);

    private void OnManualTranslateClick(object sender, RoutedEventArgs e) => ManualTranslateRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
}
