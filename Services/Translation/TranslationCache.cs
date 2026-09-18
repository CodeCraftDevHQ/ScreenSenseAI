using System.Collections.Concurrent;

namespace ScreenTranslator.Services.Translation;

public sealed class TranslationCache
{
    private readonly ConcurrentDictionary<string, string> _entries = new();
    private const int MaxEntries = 5000;

    public bool TryGet(string text, string from, string to, out string translated)
        => _entries.TryGetValue(Key(text, from, to), out translated!);

    public void Set(string text, string from, string to, string translated)
    {
        if (_entries.Count > MaxEntries)
            _entries.Clear();

        _entries[Key(text, from, to)] = translated;
    }

    public void Clear() => _entries.Clear();

    private static string Key(string text, string from, string to) => $"{from}|{to}|{text}";
}
