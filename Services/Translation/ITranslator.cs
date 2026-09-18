namespace ScreenTranslator.Services.Translation;

public interface ITranslator
{
    string Name { get; }
    bool IsConfigured { get; }
    bool SupportsOffline { get; }
    Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string from, string to, CancellationToken token = default);
}

public sealed class TranslationException : Exception
{
    public TranslationException(string message, Exception? inner = null) : base(message, inner) { }
}
