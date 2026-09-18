using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ScreenTranslator.Localization;
using ScreenTranslator.Services;
using ScreenTranslator.Services.Capture;
using ScreenTranslator.Services.Input;
using ScreenTranslator.Services.Translation;
using ScreenTranslator.Views;
using MessageBox = System.Windows.MessageBox;

namespace ScreenTranslator;

public partial class App : Application
{
    private static Mutex? _instanceMutex;

    private SettingsService _settings = null!;
    private HotkeyManager _hotkeys = null!;
    private TranslationPipeline _pipeline = null!;
    private TranslatorFactory _translatorFactory = null!;
    private FloatingIconWindow _icon = null!;
    private OverlayWindow _overlay = null!;
    private CancellationTokenSource? _running;
    private IReadOnlyList<OverlayItem> _lastItems = Array.Empty<OverlayItem>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Logger.Write("App starting.");

        _instanceMutex = new Mutex(true, "ScreenTranslator.SingleInstance", out var isNew);

        if (!isNew)
        {
            Shutdown();
            return;
        }

        _settings = new SettingsService();
        _settings.Load();

        Loc.Instance.SetLanguage(_settings.Current.UiLanguage);

        var capture = new GdiScreenCapture();
        _translatorFactory = new TranslatorFactory(_settings);

        _pipeline = new TranslationPipeline(_settings, capture, _translatorFactory);
        _pipeline.Progress += (_, args) => Dispatcher.Invoke(() => _icon.SetStatus(args.StatusKey));

        _overlay = new OverlayWindow();

        _icon = new FloatingIconWindow(_settings);
        _icon.TranslateRequested += (_, _) => RunFullScreenTranslation();
        _icon.RegionRequested += (_, _) => SelectRegion();
        _icon.SavedRegionRequested += (_, _) => RunSavedRegionTranslation();
        _icon.AdHocRegionRequested += (_, _) => SelectAdHocRegion();
        _icon.ClearRequested += (_, _) => _overlay.Clear();
        _icon.TextViewRequested += (_, _) => OpenTextView();
        _icon.ManualTranslateRequested += (_, _) => OpenManualTranslate();
        _icon.SettingsRequested += (_, _) => OpenSettings();
        _icon.ExitRequested += (_, _) => Shutdown();
        _icon.Show();

        _hotkeys = new HotkeyManager(_settings);
        _hotkeys.TranslateRequested += (_, _) => Dispatcher.Invoke(RunFullScreenTranslation);
        _hotkeys.ClearRequested += (_, _) => Dispatcher.Invoke(() => _overlay.Clear());
        _hotkeys.RegionSelectRequested += (_, _) => Dispatcher.Invoke(SelectRegion);
        _hotkeys.TranslateSavedRegionRequested += (_, _) => Dispatcher.Invoke(RunSavedRegionTranslation);
        _hotkeys.AdHocRegionRequested += (_, _) => Dispatcher.Invoke(SelectAdHocRegion);

        try
        {
            _hotkeys.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Loc.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RunFullScreenTranslation() => RunTranslation(_pipeline.ResolveFullScreenArea());

    private void RunSavedRegionTranslation()
    {
        var region = _settings.Current.SavedRegion;

        if (region is null)
        {
            var message = Loc.Instance.Language == "fa"
                ? "هنوز ناحیه‌ای انتخاب نشده. اول یک بار از دکمهٔ «انتخاب ناحیه» استفاده کن."
                : "No region selected yet. Use \"Select region\" first.";

            MessageBox.Show(message, Loc.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        RunTranslation(region.Value);
    }

    private async void RunTranslation(System.Drawing.Rectangle area)
    {
        _icon.ClosePanel();
        _overlay.Clear();

        _running?.Cancel();
        _running = new CancellationTokenSource();

        try
        {
            var items = await _pipeline.RunAsync(area, _running.Token);
            _lastItems = items;
            _overlay.ShowTranslations(items, _settings.Current, area);
        }
        catch (OperationCanceledException)
        {
        }
        catch (NotSupportedException ex)
        {
            Logger.Write("Not-implemented engine selected", ex);

            var message = Loc.Instance.Language == "fa"
                ? "این گزینه هنوز پیاده‌سازی نشده. از تنظیمات، OCR را روی «Windows built-in» و سرویس ترجمه را روی «Offline dictionary» بگذار.\n\n" + ex.Message
                : "That option isn't implemented yet. In Settings, set OCR to \"Windows built-in\" and the translation provider to \"Offline dictionary\".\n\n" + ex.Message;

            MessageBox.Show(message, Loc.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (TranslationException ex)
        {
            var message = ex.Message == "ProviderNotConfigured"
                ? Loc.Instance["ProviderNotConfigured"]
                : ex.Message;

            MessageBox.Show(message, Loc.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Loc.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectRegion()
    {
        _icon.ClosePanel();
        _overlay.Clear();
        _hotkeys.Suspend();

        System.Drawing.Rectangle? region;

        try
        {
            var window = new RegionSelectWindow();
            region = window.ShowDialog() == true ? window.SelectedRegion : null;
        }
        finally
        {
            _hotkeys.Resume();
        }

        if (region is null)
            return;

        _settings.Current.SavedRegion = region;
        _settings.Save();

        RunTranslation(region.Value);
    }

    private void SelectAdHocRegion()
    {
        _icon.ClosePanel();
        _overlay.Clear();
        _hotkeys.Suspend();

        System.Drawing.Rectangle? region;

        try
        {
            var window = new RegionSelectWindow();
            region = window.ShowDialog() == true ? window.SelectedRegion : null;
        }
        finally
        {
            _hotkeys.Resume();
        }

        if (region is null)
            return;

        RunTranslation(region.Value);
    }

    private void OpenTextView()
    {
        _icon.ClosePanel();
        _hotkeys.Suspend();

        try
        {
            new TextViewWindow(_lastItems).ShowDialog();
        }
        finally
        {
            _hotkeys.Resume();
        }
    }

    private void OpenManualTranslate()
    {
        _icon.ClosePanel();
        _hotkeys.Suspend();

        try
        {
            new ManualTranslateWindow(_settings, _translatorFactory).ShowDialog();
        }
        finally
        {
            _hotkeys.Resume();
        }
    }

    private void OpenSettings()
    {
        _icon.ClosePanel();

        var window = new SettingsWindow(_settings, _hotkeys);
        window.ShowDialog();

        _icon.Opacity = _settings.Current.FloatingIconOpacity;
        _icon.Reposition();
        _pipeline.ClearCache();
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Write("Unhandled UI exception", e.Exception);

        MessageBox.Show(
            $"{e.Exception.Message}\n\n{Logger.Path_}",
            Loc.Instance["ErrorTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Logger.Write("Unhandled non-UI exception", ex);
        else
            Logger.Write($"Unhandled non-UI exception (non-Exception payload): {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Write("Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
