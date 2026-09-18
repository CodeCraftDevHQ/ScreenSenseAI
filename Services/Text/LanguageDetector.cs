namespace ScreenTranslator.Services.Text;

public static class LanguageDetector
{
    public static string Detect(string text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        var counts = new Dictionary<string, int>();

        foreach (var ch in text)
        {
            var script = ScriptOf(ch);

            if (script is null)
                continue;

            counts[script] = counts.GetValueOrDefault(script) + 1;
        }

        if (counts.Count == 0)
            return fallback;

        var dominant = counts.OrderByDescending(x => x.Value).First().Key;

        return dominant switch
        {
            "latin" => "en",
            "arabic" => ContainsPersianOnlyLetters(text) ? "fa" : "ar",
            "cyrillic" => "ru",
            "hebrew" => "he",
            "han" => "zh-Hans",
            "kana" => "ja",
            "hangul" => "ko",
            "thai" => "th",
            "devanagari" => "hi",
            _ => fallback
        };
    }

    private static bool ContainsPersianOnlyLetters(string text)
        => text.Any(c => c is 'پ' or 'چ' or 'ژ' or 'گ' or 'ک' or 'ی');

    private static string? ScriptOf(char c)
    {
        if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z') return "latin";
        if (c is >= '\u0600' and <= '\u06FF' or >= '\uFB50' and <= '\uFDFF') return "arabic";
        if (c is >= '\u0400' and <= '\u04FF') return "cyrillic";
        if (c is >= '\u0590' and <= '\u05FF') return "hebrew";
        if (c is >= '\u4E00' and <= '\u9FFF') return "han";
        if (c is >= '\u3040' and <= '\u30FF') return "kana";
        if (c is >= '\uAC00' and <= '\uD7AF') return "hangul";
        if (c is >= '\u0E00' and <= '\u0E7F') return "thai";
        if (c is >= '\u0900' and <= '\u097F') return "devanagari";
        return null;
    }
}
