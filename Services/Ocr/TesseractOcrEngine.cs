using System.Drawing;
using System.IO;
using System.Net.Http;
using ScreenTranslator.Models;
using Tesseract;

namespace ScreenTranslator.Services.Ocr;

public sealed class TesseractOcrEngine : IOcrEngine
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromMinutes(2) };

    private readonly string _dataPath;
    private readonly TessdataDownloader _downloader;

    public string Name => "Tesseract";

    public TesseractOcrEngine(string dataPath)
    {
        _dataPath = dataPath;
        _downloader = new TessdataDownloader(SharedHttp);
    }

    public Task<IReadOnlyList<string>> GetAvailableLanguagesAsync()
    {
        IReadOnlyList<string> languages = Directory.Exists(_dataPath)
            ? Directory.GetFiles(_dataPath, "*.traineddata")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToList()
            : new List<string>();

        return Task.FromResult(languages);
    }

    public async Task<OcrResult> RecognizeAsync(Bitmap image, string? languageCode, CancellationToken token = default)
    {
        var tesseractLanguage = TesseractLanguageCodes.Resolve(languageCode);
        var dataFile = await _downloader.EnsureAsync(_dataPath, tesseractLanguage, token);

        token.ThrowIfCancellationRequested();

        using var engine = new TesseractEngine(Path.GetDirectoryName(dataFile), tesseractLanguage, EngineMode.Default);
        using var pix = ToPix(image);
        using var page = engine.Process(pix);

        var words = ExtractWords(page);
        var lines = ExtractLines(page);

        return new OcrResult
        {
            Words = words,
            Lines = lines,
            DetectedLanguage = tesseractLanguage,
            FullText = string.Join("\n", lines.Select(x => x.Text))
        };
    }

    private static List<OcrWord> ExtractWords(Page page)
    {
        var words = new List<OcrWord>();

        using var iterator = page.GetIterator();
        iterator.Begin();

        do
        {
            do
            {
                do
                {
                    do
                    {
                        if (!iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
                            continue;

                        var text = iterator.GetText(PageIteratorLevel.Word)?.Trim();

                        if (string.IsNullOrEmpty(text))
                            continue;

                        words.Add(new OcrWord
                        {
                            Text = text,
                            Bounds = new System.Windows.Rect(rect.X1, rect.Y1, rect.Width, rect.Height)
                        });
                    } while (iterator.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));
                } while (iterator.Next(PageIteratorLevel.Para, PageIteratorLevel.TextLine));
            } while (iterator.Next(PageIteratorLevel.Block, PageIteratorLevel.Para));
        } while (iterator.Next(PageIteratorLevel.Block));

        return words;
    }

    private static List<OcrBlock> ExtractLines(Page page)
    {
        var lines = new List<OcrBlock>();

        using var iterator = page.GetIterator();
        iterator.Begin();

        do
        {
            do
            {
                do
                {
                    if (!iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect))
                        continue;

                    var text = iterator.GetText(PageIteratorLevel.TextLine)?.Trim();

                    if (string.IsNullOrEmpty(text))
                        continue;

                    lines.Add(new OcrBlock
                    {
                        Text = text,
                        Bounds = new System.Windows.Rect(rect.X1, rect.Y1, rect.Width, rect.Height),
                        Confidence = iterator.GetConfidence(PageIteratorLevel.TextLine) / 100.0
                    });
                } while (iterator.Next(PageIteratorLevel.Para, PageIteratorLevel.TextLine));
            } while (iterator.Next(PageIteratorLevel.Block, PageIteratorLevel.Para));
        } while (iterator.Next(PageIteratorLevel.Block));

        return lines;
    }

    private static Pix ToPix(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        return Pix.LoadFromMemory(memory.ToArray());
    }
}
