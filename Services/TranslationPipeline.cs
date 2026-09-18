using System.Drawing;
using System.Windows;
using ScreenTranslator.Models;
using ScreenTranslator.Services.Capture;
using ScreenTranslator.Services.Ocr;
using ScreenTranslator.Services.Text;
using ScreenTranslator.Services.Translation;

namespace ScreenTranslator.Services;

public sealed class PipelineProgressEventArgs : EventArgs
{
    public string StatusKey { get; init; } = "StatusReady";
}

public sealed class TranslationPipeline
{
    private readonly SettingsService _settings;
    private readonly IScreenCapture _capture;
    private readonly TranslatorFactory _translatorFactory;
    private readonly TranslationCache _cache = new();

    public event EventHandler<PipelineProgressEventArgs>? Progress;

    public TranslationPipeline(SettingsService settings, IScreenCapture capture, TranslatorFactory translatorFactory)
    {
        _settings = settings;
        _capture = capture;
        _translatorFactory = translatorFactory;
    }

    public Rectangle ResolveFullScreenArea() => _settings.Current.CaptureActiveMonitorOnly
        ? _capture.GetActiveMonitorBounds()
        : _capture.GetVirtualScreenBounds();

    public async Task<IReadOnlyList<OverlayItem>> RunAsync(Rectangle area, CancellationToken token = default)
    {
        var s = _settings.Current;

        Report("StatusCapturing");
        using var bitmap = _capture.Capture(area);

        Report("StatusRecognizing");
        var engine = CreateOcrEngine(s);

        var ocr = await engine.RecognizeAsync(bitmap, s.SourceLanguage, token);

        var grouped = BlockGrouper.GroupIntoParagraphs(ocr.Lines);
        var blocks = BlockGrouper.FilterNoise(grouped, s.MinimumConfidence);

        if (blocks.Count == 0)
        {
            Report("StatusNoText");
            return Array.Empty<OverlayItem>();
        }

        var source = s.AutoDetectSource
            ? LanguageDetector.Detect(string.Join(" ", blocks.Select(x => x.Text)), s.SourceLanguage)
            : s.SourceLanguage;

        if (string.Equals(source, s.TargetLanguage, StringComparison.OrdinalIgnoreCase))
        {
            Report("StatusDone");
            return Array.Empty<OverlayItem>();
        }

        Report("StatusTranslating");
        var translations = await TranslateAsync(blocks, source, s.TargetLanguage, token);

        var rtl = LanguageCatalog.IsRightToLeft(s.TargetLanguage);
        var items = new List<OverlayItem>(blocks.Count);

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            var sampleArea = new Rectangle(
                (int)block.Bounds.X,
                (int)block.Bounds.Y,
                (int)block.Bounds.Width,
                (int)block.Bounds.Height);

            var (background, foreground) = ColorSampler.Sample(bitmap, sampleArea);

            items.Add(new OverlayItem
            {
                OriginalText = block.Text,
                TranslatedText = translations[i],
                ScreenBounds = new Rect(
                    block.Bounds.X + area.X,
                    block.Bounds.Y + area.Y,
                    block.Bounds.Width,
                    block.Bounds.Height),
                Background = background,
                Foreground = foreground,
                IsRightToLeft = rtl
            });
        }

        Report("StatusDone");
        return items;
    }

    private async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<OcrBlock> blocks, string from, string to, CancellationToken token)
    {
        var useCache = _settings.Current.CacheTranslations;
        var results = new string[blocks.Count];
        var pending = new List<int>();

        for (var i = 0; i < blocks.Count; i++)
        {
            if (useCache && _cache.TryGet(blocks[i].Text, from, to, out var cached))
                results[i] = cached;
            else
                pending.Add(i);
        }

        if (pending.Count > 0)
        {
            var translator = _translatorFactory.Create();

            if (!translator.IsConfigured)
                throw new TranslationException("ProviderNotConfigured");

            const int batchSize = 25;

            for (var offset = 0; offset < pending.Count; offset += batchSize)
            {
                token.ThrowIfCancellationRequested();

                var slice = pending.Skip(offset).Take(batchSize).ToList();
                var inputs = slice.Select(i => blocks[i].Text).ToList();
                var translated = await translator.TranslateAsync(inputs, from, to, token);

                for (var j = 0; j < slice.Count; j++)
                {
                    var value = j < translated.Count ? translated[j] : inputs[j];
                    results[slice[j]] = value;

                    if (useCache)
                        _cache.Set(inputs[j], from, to, value);
                }
            }
        }

        return results;
    }

    private static IOcrEngine CreateOcrEngine(AppSettings settings) => settings.OcrEngine switch
    {
        OcrEngineKind.Tesseract => new TesseractOcrEngine(settings.TesseractDataPath),
        _ => new WindowsOcrEngine()
    };

    public void ClearCache() => _cache.Clear();

    private void Report(string key) => Progress?.Invoke(this, new PipelineProgressEventArgs { StatusKey = key });
}
