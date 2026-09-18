using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ScreenTranslator.Services.Translation;

public sealed class AzureTranslator : ITranslator
{
    private const string Endpoint = "https://api.cognitive.microsofttranslator.com";

    private readonly HttpClient _http;
    private readonly string _key;
    private readonly string _region;

    public string Name => "Azure Translator";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_key);
    public bool SupportsOffline => false;

    public AzureTranslator(HttpClient http, string key, string region)
    {
        _http = http;
        _key = key;
        _region = string.IsNullOrWhiteSpace(region) ? "global" : region;
    }

    public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string from, string to, CancellationToken token = default)
    {
        if (!IsConfigured)
            throw new TranslationException("Azure Translator key is not set.");

        var url = $"{Endpoint}/translate?api-version=3.0&to={Uri.EscapeDataString(to)}";

        if (!string.IsNullOrWhiteSpace(from) && from != "auto")
            url += $"&from={Uri.EscapeDataString(from)}";

        var payload = texts.Select(t => new { Text = t }).ToArray();

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
        request.Headers.Add("Ocp-Apim-Subscription-Region", _region);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
            throw new TranslationException($"Azure returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        var results = new List<string>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var translation = element.GetProperty("translations")[0].GetProperty("text").GetString();
            results.Add(translation ?? string.Empty);
        }

        return results;
    }
}
