using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenTranslator.Localization;
using ScreenTranslator.Models;
using ScreenTranslator.Services;
using ScreenTranslator.Services.Input;
using ScreenTranslator.Services.Ocr;
using ScreenTranslator.Services.Translation;
using MessageBox = System.Windows.MessageBox;

namespace ScreenTranslator.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private AppSettings _draft;
    private readonly HotkeyManager _hotkeys;
    private string? _recordingTarget;
    private string? _cloudShownPreset;

    public SettingsWindow(SettingsService settings, HotkeyManager hotkeys)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _draft = Clone(settings.Current);

        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => _hotkeys.Resume();

        _hotkeys.Suspend();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        foreach (var language in LanguageCatalog.All)
        {
            SourceLanguageBox.Items.Add(language);
            TargetLanguageBox.Items.Add(language);
        }

        foreach (var preset in CloudAiPresets.All)
            CloudAiPresetBox.Items.Add(new ComboBoxItem { Content = preset.DisplayName, Tag = preset.Id });

        foreach (var family in Fonts.SystemFontFamilies.OrderBy(x => x.Source))
            FontBox.Items.Add(family.Source);

        PopulateFromDraft();
        RefreshProviderUi();

        NoticesBox.Text = LoadNotices();

        var engine = new WindowsOcrEngine();
        var languages = await engine.GetAvailableLanguagesAsync();
        OcrLanguagesText.Text = string.Join(", ", languages);
    }

    private void PopulateFromDraft()
    {
        SourceLanguageBox.SelectedItem = LanguageCatalog.Find(_draft.SourceLanguage);
        TargetLanguageBox.SelectedItem = LanguageCatalog.Find(_draft.TargetLanguage);

        SelectByTag(UiLanguageBox, _draft.UiLanguage);
        SelectByTag(OcrEngineBox, _draft.OcrEngine.ToString());
        SelectByTag(ProviderBox, _draft.Provider.ToString());

        StartWithWindowsBox.IsChecked = _draft.StartWithWindows;
        ActiveMonitorBox.IsChecked = _draft.CaptureActiveMonitorOnly;
        CacheBox.IsChecked = _draft.CacheTranslations;
        IconOpacitySlider.Value = _draft.FloatingIconOpacity;

        AutoDetectBox.IsChecked = _draft.AutoDetectSource;

        TranslateTriggerBox.Text = _draft.TranslateTrigger.ToString();
        ClearTriggerBox.Text = _draft.ClearOverlayTrigger.ToString();
        RegionTriggerBox.Text = _draft.RegionSelectTrigger.ToString();
        SavedRegionTriggerBox.Text = _draft.TranslateSavedRegionTrigger.ToString();
        AdHocRegionTriggerBox.Text = _draft.AdHocRegionTrigger.ToString();
        CtrlBox.IsChecked = _draft.HotkeyRequiresControl;
        AltBox.IsChecked = _draft.HotkeyRequiresAlt;
        ShiftBox.IsChecked = _draft.HotkeyRequiresShift;
        SwallowBox.IsChecked = _draft.SwallowHotkey;

        TessdataBox.Text = _draft.TesseractDataPath;
        ConfidenceSlider.Value = _draft.MinimumConfidence;

        AzureKeyBox.Text = _draft.AzureKey;
        AzureRegionBox.Text = _draft.AzureRegion;
        DeepLKeyBox.Text = _draft.DeepLKey;
        LibreUrlBox.Text = _draft.LibreTranslateUrl;
        LibreKeyBox.Text = _draft.LibreTranslateKey;
        OllamaUrlBox.Text = _draft.OllamaUrl;
        OllamaModelBox.Text = _draft.OllamaModel;
        OllamaTemperatureSlider.Value = _draft.OllamaTemperature;
        OllamaPromptBox.Text = _draft.OllamaPromptTemplate;
        LmStudioUrlBox.Text = _draft.LmStudioUrl;
        LmStudioModelBox.Text = _draft.LmStudioModel;
        LmStudioTemperatureSlider.Value = _draft.LmStudioTemperature;
        LmStudioPromptBox.Text = _draft.LmStudioPromptTemplate;

        CloudAiTemperatureSlider.Value = _draft.CloudAiTemperature;
        CloudAiExtraBox.Text = _draft.CloudAiExtraInstructions;
        _cloudShownPreset = null;
        SelectByTag(CloudAiPresetBox, _draft.CloudAiPreset);
        _cloudShownPreset = CurrentCloudPresetId();
        LoadCloudProfile(_cloudShownPreset);

        FontBox.Text = _draft.OverlayFontFamily;
        OverlayOpacitySlider.Value = _draft.OverlayOpacity;
        MaxFontSlider.Value = _draft.MaxFontSize;
        HoverOriginalBox.IsChecked = _draft.ShowOriginalOnHover;
        AutoHideBox.IsChecked = _draft.AutoHideOverlay;
        TimeoutSlider.Value = _draft.OverlayTimeoutSeconds;
    }

    private void OnUiLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = (UiLanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        if (!string.IsNullOrEmpty(tag))
            Loc.Instance.SetLanguage(tag);

        RefreshProviderUi();
    }

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e) => RefreshProviderUi();

    private void RefreshProviderUi()
    {
        var tag = (ProviderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        AzurePanel.Visibility = tag == "Azure" ? Visibility.Visible : Visibility.Collapsed;
        DeepLPanel.Visibility = tag == "DeepL" ? Visibility.Visible : Visibility.Collapsed;
        LibrePanel.Visibility = tag == "LibreTranslate" ? Visibility.Visible : Visibility.Collapsed;
        OllamaPanel.Visibility = tag == "Ollama" ? Visibility.Visible : Visibility.Collapsed;
        LmStudioPanel.Visibility = tag == "LmStudio" ? Visibility.Visible : Visibility.Collapsed;
        CloudAiPanel.Visibility = tag == "CloudAi" ? Visibility.Visible : Visibility.Collapsed;

        ProviderHintText.Text = tag switch
        {
            "OfflineDictionary" => Loc.Instance.Language == "fa"
                ? "کاملاً آفلاین و بدون تنظیمات کار می‌کند، ولی چون کلمه‌به‌کلمه ترجمه می‌کند کیفیتش پایین است. فقط بین انگلیسی و فارسی."
                : "Fully offline, no setup. Word-by-word, so quality is rough. English <-> Persian only.",
            "LibreTranslate" => Loc.Instance.Language == "fa"
                ? "به یک سرور LibreTranslate نیاز دارد. می‌تواند روی خود سیستم تو اجرا شود (کاملاً آفلاین بعد از نصب) یا یک سرور دیگر."
                : "Needs a LibreTranslate server. Can run on your own machine (fully offline after install) or point at another server.",
            "Ollama" => Loc.Instance.Language == "fa"
                ? "به Ollama و یک مدل محلی (مثل gemma3n:e2b) نیاز دارد که خودت نصب و اجرا می‌کنی. کاملاً آفلاین، کیفیتش از همه بهتر است، ولی چون مدل زبانی واقعی است کندتر از بقیه جواب می‌دهد."
                : "Needs Ollama plus a local model (e.g. gemma3n:e2b) that you install and run yourself. Fully offline, the best quality here, but slower since it's a real language model.",
            "LmStudio" => Loc.Instance.Language == "fa"
                ? "به LM Studio نیاز دارد: مدلی را در آن بارگذاری کن و از تب Developer/Local Server دکمهٔ Start Server را بزن. آدرس پیش‌فرض http://localhost:1234 است."
                : "Needs LM Studio: load a model in it and press Start Server on the Developer/Local Server tab. Default address is http://localhost:1234.",
            "CloudAi" => Loc.Instance.Language == "fa"
                ? "از هر سرویس هوش مصنوعی که کلید API دارید (Gemini، ChatGPT، Claude، OpenRouter، Groq و غیره) برای ترجمه استفاده می‌کند. به اینترنت نیاز دارد و ترجمه با کیفیت بالا انجام می‌شود."
                : "Uses any AI service you have an API key for (Gemini, ChatGPT, Claude, OpenRouter, Groq, ...) to translate. Needs internet; quality is high.",
            "Azure" or "DeepL" => Loc.Instance.Language == "fa"
                ? "به اینترنت و یک کلید API رایگان از سرویس‌دهنده نیاز دارد."
                : "Needs internet and a free API key from the provider.",
            _ => string.Empty
        };
    }

    private string CurrentCloudPresetId() =>
        (CloudAiPresetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? CloudAiPresets.All[0].Id;

    private void OnCloudPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_cloudShownPreset is not null)
            SaveCloudProfile(_draft, _cloudShownPreset);

        _cloudShownPreset = CurrentCloudPresetId();
        LoadCloudProfile(_cloudShownPreset);
    }

    private void LoadCloudProfile(string id)
    {
        var preset = CloudAiPresets.Get(id);
        _draft.CloudAiProfiles.TryGetValue(preset.Id, out var profile);

        CloudAiKeyBox.Password = profile?.Key ?? string.Empty;
        CloudAiModelBox.Items.Clear();
        CloudAiModelBox.Text = string.IsNullOrWhiteSpace(profile?.Model) ? preset.DefaultModel : profile.Model;
        CloudAiBaseUrlBox.Text = string.IsNullOrWhiteSpace(profile?.BaseUrl) ? preset.DefaultBaseUrl : profile.BaseUrl;
        CloudAiBaseUrlPanel.Visibility = preset.Id == "Custom" ? Visibility.Visible : Visibility.Collapsed;

        CloudAiHelpText.Text = string.IsNullOrEmpty(preset.KeyHelp)
            ? string.Empty
            : (Loc.Instance.Language == "fa" ? "دریافت کلید: " : "Get a key: ") + preset.KeyHelp;
    }

    private void SaveCloudProfile(AppSettings target, string id)
    {
        var preset = CloudAiPresets.Get(id);

        target.CloudAiProfiles[preset.Id] = new CloudAiProfile
        {
            Key = CloudAiKeyBox.Password.Trim(),
            Model = CloudAiModelBox.Text.Trim(),
            BaseUrl = CloudAiBaseUrlBox.Text.Trim()
        };
    }

    private async void OnLoadModels(object sender, RoutedEventArgs e)
    {
        var preset = CloudAiPresets.Get(CurrentCloudPresetId());

        try
        {
            LoadModelsButton.IsEnabled = false;
            TestResultText.Foreground = System.Windows.Media.Brushes.Gray;
            TestResultText.Text = "...";

            var factory = new TranslatorFactory(_settings);

            var models = await CloudAiTranslator.ListModelsAsync(
                factory.CloudAiHttp,
                preset,
                CloudAiKeyBox.Password,
                CloudAiBaseUrlBox.Text);

            var current = CloudAiModelBox.Text;
            CloudAiModelBox.Items.Clear();

            foreach (var model in models)
                CloudAiModelBox.Items.Add(model);

            CloudAiModelBox.Text = current;
            CloudAiModelBox.IsDropDownOpen = true;

            TestResultText.Foreground = System.Windows.Media.Brushes.SeaGreen;
            TestResultText.Text = Loc.Instance.Language == "fa"
                ? $"{models.Count} مدل پیدا شد."
                : $"{models.Count} models found.";
        }
        catch (Exception ex)
        {
            TestResultText.Foreground = System.Windows.Media.Brushes.IndianRed;
            TestResultText.Text = ex.Message;
        }
        finally
        {
            LoadModelsButton.IsEnabled = true;
        }
    }

    private void OnSwap(object sender, RoutedEventArgs e)
    {
        var source = SourceLanguageBox.SelectedItem;
        SourceLanguageBox.SelectedItem = TargetLanguageBox.SelectedItem;
        TargetLanguageBox.SelectedItem = source;
    }

    private void OnResetToDefaults(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            Loc.Instance["ConfirmReset"],
            Loc.Instance["ResetToDefaults"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _draft = new AppSettings();
        PopulateFromDraft();
        RefreshProviderUi();
    }

    private void OnRecordClick(object sender, RoutedEventArgs e)
    {
        _recordingTarget = (sender as Button)?.Tag?.ToString();
        RecordingHintText.Text = Loc.Instance["PressAnyKey"];
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingTarget is null)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
            return;

        e.Handled = true;
        var formsKey = (System.Windows.Forms.Keys)KeyInterop.VirtualKeyFromKey(key);
        ApplyTrigger(HotkeyTrigger.FromKey(formsKey));
    }

    private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_recordingTarget is null)
            return;

        TriggerMouseButton? button = e.ChangedButton switch
        {
            System.Windows.Input.MouseButton.Left => TriggerMouseButton.Left,
            System.Windows.Input.MouseButton.Middle => TriggerMouseButton.Middle,
            System.Windows.Input.MouseButton.Right => TriggerMouseButton.Right,
            System.Windows.Input.MouseButton.XButton1 => TriggerMouseButton.XButton1,
            System.Windows.Input.MouseButton.XButton2 => TriggerMouseButton.XButton2,
            _ => null
        };

        if (button is null)
            return;

        e.Handled = true;
        ApplyTrigger(new HotkeyTrigger { Kind = TriggerKind.MouseButton, MouseButton = button.Value });
    }

    private void OnWindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_recordingTarget is null)
            return;

        e.Handled = true;
        ApplyTrigger(new HotkeyTrigger { Kind = e.Delta > 0 ? TriggerKind.WheelUp : TriggerKind.WheelDown });
    }

    private void ApplyTrigger(HotkeyTrigger trigger)
    {
        switch (_recordingTarget)
        {
            case "translate":
                _draft.TranslateTrigger = trigger;
                TranslateTriggerBox.Text = trigger.ToString();
                break;
            case "clear":
                _draft.ClearOverlayTrigger = trigger;
                ClearTriggerBox.Text = trigger.ToString();
                break;
            case "region":
                _draft.RegionSelectTrigger = trigger;
                RegionTriggerBox.Text = trigger.ToString();
                break;
            case "savedregion":
                _draft.TranslateSavedRegionTrigger = trigger;
                SavedRegionTriggerBox.Text = trigger.ToString();
                break;
            case "adhocregion":
                _draft.AdHocRegionTrigger = trigger;
                AdHocRegionTriggerBox.Text = trigger.ToString();
                break;
        }

        _recordingTarget = null;
        RecordingHintText.Text = string.Empty;
    }

    private void OnBrowseTessdata(object sender, RoutedEventArgs e)
    {
        var path = PickFolder();

        if (path is not null)
            TessdataBox.Text = path;
    }

    private static string? PickFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private async void OnTestProvider(object sender, RoutedEventArgs e)
    {
        CollectInto(_draft);

        var temporary = SettingsService.InMemory(_draft);

        var factory = new TranslatorFactory(temporary);
        var translator = factory.Create(_draft.Provider);

        try
        {
            TestResultText.Foreground = System.Windows.Media.Brushes.Gray;
            TestResultText.Text = "...";

            var result = await translator.TranslateAsync(new[] { "Hello world" }, _draft.SourceLanguage, _draft.TargetLanguage);

            TestResultText.Foreground = System.Windows.Media.Brushes.SeaGreen;
            TestResultText.Text = result.FirstOrDefault() ?? string.Empty;
        }
        catch (Exception ex)
        {
            TestResultText.Foreground = System.Windows.Media.Brushes.IndianRed;
            TestResultText.Text = ex.Message;
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        CollectInto(_draft);
        _settings.Replace(_draft);
        Loc.Instance.SetLanguage(_draft.UiLanguage);
        StartupRegistration.Apply(_draft.StartWithWindows);
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Loc.Instance.SetLanguage(_settings.Current.UiLanguage);
        DialogResult = false;
        Close();
    }

    private void CollectInto(AppSettings target)
    {
        target.UiLanguage = (UiLanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
        target.SourceLanguage = (SourceLanguageBox.SelectedItem as LanguageItem)?.Code ?? "en";
        target.TargetLanguage = (TargetLanguageBox.SelectedItem as LanguageItem)?.Code ?? "fa";
        target.AutoDetectSource = AutoDetectBox.IsChecked == true;

        target.HotkeyRequiresControl = CtrlBox.IsChecked == true;
        target.HotkeyRequiresAlt = AltBox.IsChecked == true;
        target.HotkeyRequiresShift = ShiftBox.IsChecked == true;
        target.SwallowHotkey = SwallowBox.IsChecked == true;

        target.OcrEngine = Enum.Parse<OcrEngineKind>((OcrEngineBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Tesseract");
        target.TesseractDataPath = TessdataBox.Text.Trim();
        target.MinimumConfidence = ConfidenceSlider.Value;

        target.Provider = Enum.Parse<TranslationProviderKind>((ProviderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "OfflineDictionary");
        target.AzureKey = AzureKeyBox.Text.Trim();
        target.AzureRegion = AzureRegionBox.Text.Trim();
        target.DeepLKey = DeepLKeyBox.Text.Trim();
        target.LibreTranslateUrl = LibreUrlBox.Text.Trim();
        target.LibreTranslateKey = LibreKeyBox.Text.Trim();
        target.OllamaUrl = OllamaUrlBox.Text.Trim();
        target.OllamaModel = OllamaModelBox.Text.Trim();
        target.OllamaTemperature = OllamaTemperatureSlider.Value;
        target.OllamaPromptTemplate = string.IsNullOrWhiteSpace(OllamaPromptBox.Text) ? AppSettings.DefaultPromptTemplate : OllamaPromptBox.Text;
        target.LmStudioUrl = LmStudioUrlBox.Text.Trim();
        target.LmStudioModel = LmStudioModelBox.Text.Trim();
        target.LmStudioTemperature = LmStudioTemperatureSlider.Value;
        target.LmStudioPromptTemplate = string.IsNullOrWhiteSpace(LmStudioPromptBox.Text) ? AppSettings.DefaultPromptTemplate : LmStudioPromptBox.Text;

        target.CloudAiTemperature = CloudAiTemperatureSlider.Value;
        target.CloudAiExtraInstructions = CloudAiExtraBox.Text.Trim();
        target.CloudAiPreset = CurrentCloudPresetId();
        SaveCloudProfile(target, target.CloudAiPreset);

        target.OverlayFontFamily = string.IsNullOrWhiteSpace(FontBox.Text) ? "Segoe UI" : FontBox.Text;
        target.OverlayOpacity = OverlayOpacitySlider.Value;
        target.MaxFontSize = MaxFontSlider.Value;
        target.ShowOriginalOnHover = HoverOriginalBox.IsChecked == true;
        target.AutoHideOverlay = AutoHideBox.IsChecked == true;
        target.OverlayTimeoutSeconds = (int)TimeoutSlider.Value;

        target.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        target.CaptureActiveMonitorOnly = ActiveMonitorBox.IsChecked == true;
        target.CacheTranslations = CacheBox.IsChecked == true;
        target.FloatingIconOpacity = IconOpacitySlider.Value;
    }

    private static void SelectByTag(System.Windows.Controls.ComboBox box, string tag)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }

        box.SelectedIndex = 0;
    }

    private static string LoadNotices()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static AppSettings Clone(AppSettings source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}
