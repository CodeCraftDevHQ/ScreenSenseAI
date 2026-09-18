using System.IO;

namespace ScreenTranslator.Services.Translation;

public sealed class OfflineTranslator : ITranslator
{
    private readonly string _modelsRoot;

    public string Name => "Offline (ONNX)";
    public bool SupportsOffline => true;
    public bool IsConfigured => Directory.Exists(_modelsRoot);

    public OfflineTranslator(string modelsRoot)
    {
        _modelsRoot = modelsRoot;
    }

    public IReadOnlyList<string> InstalledPairs()
    {
        if (!Directory.Exists(_modelsRoot))
            return Array.Empty<string>();

        return Directory.GetDirectories(_modelsRoot)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x!)
            .ToList();
    }

    public string? ResolveModelPath(string from, string to)
    {
        var folder = Path.Combine(_modelsRoot, $"{from}-{to}");
        return Directory.Exists(folder) ? folder : null;
    }

    public Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string from, string to, CancellationToken token = default)
    {
        var modelPath = ResolveModelPath(from, to);

        if (modelPath is null)
            throw new TranslationException($"No offline model installed for {from}-{to}.");

        throw new NotSupportedException(
            "Place an OPUS-MT encoder.onnx, decoder.onnx and the SentencePiece vocabulary in the model folder, then run them through Microsoft.ML.OnnxRuntime here.");
    }
}
