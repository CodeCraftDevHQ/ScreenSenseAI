using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ScreenTranslator.Services.Translation;

public sealed class LibreTranslateTranslator : ITranslator
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public string Name => "LibreTranslate";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl);
    public bool SupportsOffline => false;

    public LibreTranslateTranslator(HttpClient http, string baseUrl, string apiKey)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
    }

    public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string from, string to, CancellationToken token = default)
    {
        if (!IsConfigured)
            throw new TranslationException("LibreTranslate server URL is not set.");

        var payload = new Dictionary<string, object>
        {
            ["q"] = texts,
            ["source"] = string.IsNullOrWhiteSpace(from) ? "auto" : Normalize(from),
            ["target"] = Normalize(to),
            ["format"] = "text"
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
            payload["api_key"] = _apiKey;

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"{_baseUrl}/translate", content, token);

        var body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
            throw new TranslationException($"LibreTranslate returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        var translated = document.RootElement.GetProperty("translatedText");

        if (translated.ValueKind == JsonValueKind.Array)
            return translated.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();

        return new List<string> { translated.GetString() ?? string.Empty };
    }

    private static string Normalize(string code)
    {
        var index = code.IndexOf('-');
        return index > 0 ? code[..index] : code;
    }
}
