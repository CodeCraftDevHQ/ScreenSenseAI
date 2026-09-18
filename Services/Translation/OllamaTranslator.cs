using System.Net.Http;
using System.Text;
using System.Text.Json;
using ScreenTranslator.Models;
using ScreenTranslator.Services;

namespace ScreenTranslator.Services.Translation;

public sealed class OllamaTranslator : ITranslator
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly double _temperature;
    private readonly string _promptTemplate;

    public string Name => "Ollama (local LLM)";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrl) && !string.IsNullOrWhiteSpace(_model);
    public bool SupportsOffline => true;

    public OllamaTranslator(HttpClient http, string baseUrl, string model, double temperature, string promptTemplate)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _temperature = temperature;
        _promptTemplate = string.IsNullOrWhiteSpace(promptTemplate) ? AppSettings.DefaultPromptTemplate : promptTemplate;
    }

    public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string from, string to, CancellationToken token = default)
    {
        if (!IsConfigured)
            throw new TranslationException("Ollama server URL or model name is not set.");

        var fromName = LanguageCatalog.Find(from)?.EnglishName ?? from;
        var toName = LanguageCatalog.Find(to)?.EnglishName ?? to;

        var results = new List<string>(texts.Count);

        foreach (var text in texts)
        {
            token.ThrowIfCancellationRequested();
            results.Add(await TranslateOneAsync(text, fromName, toName, token));
        }

        return results;
    }

    private async Task<string> TranslateOneAsync(string text, string fromName, string toName, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var prompt = _promptTemplate
            .Replace("{from}", fromName)
            .Replace("{to}", toName)
            .Replace("{text}", text);

        var payload = new
        {
            model = _model,
            prompt,
            stream = false,
            options = new { temperature = _temperature }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response;

        try
        {
            response = await _http.PostAsync($"{_baseUrl}/api/generate", content, token);
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException(
                $"Could not reach Ollama at {_baseUrl}. Is Ollama running (\"ollama serve\") and is the model pulled?", ex);
        }

        var body = await response.Content.ReadAsStringAsync(token);

        if (!response.IsSuccessStatusCode)
            throw new TranslationException($"Ollama returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        var raw = document.RootElement.TryGetProperty("response", out var responseField)
            ? responseField.GetString() ?? string.Empty
            : string.Empty;

        return CleanUp(raw);
    }

    private static string CleanUp(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            trimmed = trimmed[1..^1];

        return trimmed;
    }
}
