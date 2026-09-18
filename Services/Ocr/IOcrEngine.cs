using System.Drawing;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Ocr;

public interface IOcrEngine
{
    string Name { get; }
    Task<IReadOnlyList<string>> GetAvailableLanguagesAsync();
    Task<OcrResult> RecognizeAsync(Bitmap image, string? languageCode, CancellationToken token = default);
}

public sealed class OcrResult
{
    public IReadOnlyList<OcrWord> Words { get; init; } = Array.Empty<OcrWord>();
    public IReadOnlyList<OcrBlock> Lines { get; init; } = Array.Empty<OcrBlock>();
    public string DetectedLanguage { get; init; } = string.Empty;
    public string FullText { get; init; } = string.Empty;
}
