using System.IO;
using System.Windows.Forms;

namespace ScreenTranslator.Models;

public sealed class AppSettings
{
    public string UiLanguage { get; set; } = "en";
    public string SourceLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "fa";
    public bool AutoDetectSource { get; set; } = true;

    public HotkeyTrigger TranslateTrigger { get; set; } = HotkeyTrigger.FromKey(Keys.NumPad1);
    public HotkeyTrigger ClearOverlayTrigger { get; set; } = HotkeyTrigger.FromKey(Keys.Escape);
    public HotkeyTrigger RegionSelectTrigger { get; set; } = HotkeyTrigger.FromKey(Keys.NumPad2);
    public HotkeyTrigger TranslateSavedRegionTrigger { get; set; } = HotkeyTrigger.FromKey(Keys.NumPad3);
    public HotkeyTrigger AdHocRegionTrigger { get; set; } = HotkeyTrigger.FromKey(Keys.NumPad4);
    public bool HotkeyRequiresControl { get; set; }
    public bool HotkeyRequiresAlt { get; set; }
    public bool HotkeyRequiresShift { get; set; }
    public bool SwallowHotkey { get; set; } = true;

    public OcrEngineKind OcrEngine { get; set; } = OcrEngineKind.Tesseract;
    public string TesseractDataPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTranslator", "tessdata");
    public double MinimumConfidence { get; set; } = 0.35;

    public const string DefaultPromptTemplate =
        "Translate the following text from {from} to {to}. Output ONLY the translation, with no quotes, no explanation, and no extra commentary.\n\nText: {text}";

    public TranslationProviderKind Provider { get; set; } = TranslationProviderKind.OfflineDictionary;
    public string AzureKey { get; set; } = string.Empty;
    public string AzureRegion { get; set; } = "global";
    public string DeepLKey { get; set; } = string.Empty;
    public string LibreTranslateUrl { get; set; } = "http://localhost:5000";
    public string LibreTranslateKey { get; set; } = string.Empty;
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "gemma3n:e2b";
    public double OllamaTemperature { get; set; } = 0.2;
    public string OllamaPromptTemplate { get; set; } = DefaultPromptTemplate;
    public string LmStudioUrl { get; set; } = "http://localhost:1234";
    public string LmStudioModel { get; set; } = string.Empty;
    public double LmStudioTemperature { get; set; } = 0.2;
    public string LmStudioPromptTemplate { get; set; } = DefaultPromptTemplate;
    public string OfflineModelsPath { get; set; } = string.Empty;

    public double OverlayOpacity { get; set; } = 1.0;
    public bool ShowOriginalOnHover { get; set; } = true;
    public string OverlayFontFamily { get; set; } = "Segoe UI";
    public double MaxFontSize { get; set; } = 48;
    public bool AutoHideOverlay { get; set; }
    public int OverlayTimeoutSeconds { get; set; } = 30;

    public bool StartWithWindows { get; set; }
    public bool CaptureActiveMonitorOnly { get; set; } = true;
    public double FloatingIconOpacity { get; set; } = 0.85;
    public double FloatingIconTop { get; set; } = 300;
    public double FloatingIconLeft { get; set; } = 4;
    public bool FloatingIconOnRightEdge { get; set; } = true;
    public bool CacheTranslations { get; set; } = true;

    public int? SavedRegionX { get; set; }
    public int? SavedRegionY { get; set; }
    public int? SavedRegionWidth { get; set; }
    public int? SavedRegionHeight { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public System.Drawing.Rectangle? SavedRegion
    {
        get => SavedRegionWidth.HasValue && SavedRegionHeight.HasValue
            ? new System.Drawing.Rectangle(SavedRegionX ?? 0, SavedRegionY ?? 0, SavedRegionWidth.Value, SavedRegionHeight.Value)
            : null;
        set
        {
            SavedRegionX = value?.X;
            SavedRegionY = value?.Y;
            SavedRegionWidth = value?.Width;
            SavedRegionHeight = value?.Height;
        }
    }
}
