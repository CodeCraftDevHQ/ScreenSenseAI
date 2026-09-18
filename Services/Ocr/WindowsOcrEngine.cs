using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Linq;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ScreenTranslator.Services.Ocr;

public sealed class WindowsOcrEngine : IOcrEngine
{
    public string Name => "Windows.Media.Ocr";

    public Task<IReadOnlyList<string>> GetAvailableLanguagesAsync()
    {
        IReadOnlyList<string> languages = OcrEngine
            .AvailableRecognizerLanguages
            .Select(x => x.LanguageTag)
            .ToList();

        return Task.FromResult(languages);
    }

    public async Task<OcrResult> RecognizeAsync(Bitmap image, string? languageCode, CancellationToken token = default)
    {
        var engine = CreateEngine(languageCode);

        if (engine is null)
            throw new InvalidOperationException($"No Windows OCR language pack available for '{languageCode}'.");

        using var softwareBitmap = await ToSoftwareBitmapAsync(image);
        var ocr = await engine.RecognizeAsync(softwareBitmap);

        var words = new List<ScreenTranslator.Models.OcrWord>();
        var lines = new List<ScreenTranslator.Models.OcrBlock>();

        foreach (var line in ocr.Lines)
        {
            token.ThrowIfCancellationRequested();

            double left = double.MaxValue, top = double.MaxValue, right = 0, bottom = 0;

            foreach (var word in line.Words)
            {
                var rect = new Rect(word.BoundingRect.X, word.BoundingRect.Y, word.BoundingRect.Width, word.BoundingRect.Height);
                words.Add(new ScreenTranslator.Models.OcrWord { Text = word.Text, Bounds = rect });

                left = Math.Min(left, rect.Left);
                top = Math.Min(top, rect.Top);
                right = Math.Max(right, rect.Right);
                bottom = Math.Max(bottom, rect.Bottom);
            }

            if (line.Words.Count == 0)
                continue;

            lines.Add(new ScreenTranslator.Models.OcrBlock
            {
                Text = line.Text,
                Bounds = new Rect(left, top, right - left, bottom - top),
                Confidence = 1.0
            });
        }

        return new OcrResult
        {
            Words = words,
            Lines = lines,
            DetectedLanguage = engine.RecognizerLanguage.LanguageTag,
            FullText = string.Join("\n", lines.Select(x => x.Text))
        };
    }

    private static OcrEngine? CreateEngine(string? languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            try
            {
                var language = new Language(languageCode);
                var engine = OcrEngine.TryCreateFromLanguage(language);

                if (engine is not null)
                    return engine;
            }
            catch (Exception)
            {
            }
        }

        return OcrEngine.TryCreateFromUserProfileLanguages();
    }

    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(memory.ToArray());
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }
}
