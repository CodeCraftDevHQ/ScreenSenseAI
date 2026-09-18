using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ScreenTranslator.Services.Translation;

public sealed class DeepLTranslator : ITranslator
{
    private readonly HttpClient _http;
    private readonly string _key;

    public string Name => "DeepL";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_key);
    public bool SupportsOffline => false;

    public DeepLTranslator(HttpClient http, string key)
    {
        _http = http;
        _key = key;
    }

    private string BaseUrl => _key.EndsWith(":fx", StringComparison.Ordinal)
        ? "https://api-free.deepl.com"
        : "https://api.deepl.com";

    public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string from, string to, CancellationToken token = default)
    {
        if (!IsConfigured)
            throw new TranslationException("DeepL API key is not set.");

        var payload = new Dictionary<string, object>
        {
            ["text"] = texts,
            ["target_lang"] = to.ToUpperInvariant()
        };

        if (!string.IsNullOrWhiteSpace(from) && from != "auto")
            payload["source_lang"] = from.ToUpperInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/translate");
        request.Headers.Add("Authorization", $"DeepL-Auth-Key {_key}");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
            throw new TranslationException($"DeepL returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement
            .GetProperty("translations")
            .EnumerateArray()
            .Select(x => x.GetProperty("text").GetString() ?? string.Empty)
            .ToList();
    }
}
