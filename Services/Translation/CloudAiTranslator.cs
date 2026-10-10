using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ScreenTranslator.Services;

namespace ScreenTranslator.Services.Translation;

public sealed class CloudAiTranslator : ITranslator
{
    private const int MaxAttempts = 3;

    private readonly HttpClient _http;
    private readonly CloudAiPresetInfo _preset;
    private readonly string _key;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly double _temperature;
    private readonly string _extraInstructions;

    public string Name => $"{_preset.DisplayName} (cloud AI)";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_key)
                                && !string.IsNullOrWhiteSpace(_model)
                                && !string.IsNullOrWhiteSpace(_baseUrl);
    public bool SupportsOffline => false;

    public CloudAiTranslator(
        HttpClient http,
        CloudAiPresetInfo preset,
        string key,
        string model,
        string baseUrl,
        double temperature,
        string extraInstructions)
    {
        _http = http;
        _preset = preset;
        _key = key.Trim();
        _model = NormalizeModel(model);
        _baseUrl = baseUrl.Trim().TrimEnd('/');
        _temperature = temperature;
        _extraInstructions = extraInstructions?.Trim() ?? string.Empty;
    }

    public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> texts, string from, string to, CancellationToken token = default)
    {
        if (!IsConfigured)
            throw new TranslationException("Cloud AI is not configured. Open Settings, choose the service and enter your API key and model.");

        var fromName = LanguageCatalog.Find(from)?.EnglishName ?? from;
        var toName = LanguageCatalog.Find(to)?.EnglishName ?? to;

        var results = texts.ToArray();
        var indexes = new List<int>();

        for (var i = 0; i < texts.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(texts[i]))
                indexes.Add(i);
        }

        if (indexes.Count == 0)
            return results;

        var inputs = indexes.Select(i => texts[i]).ToList();

        var raw = await SendAsync(BatchSystemPrompt(fromName, toName), JsonSerializer.Serialize(inputs), true, token);
        var parsed = TryParseArray(raw, inputs.Count);

        if (parsed is not null)
        {
            for (var j = 0; j < indexes.Count; j++)
                results[indexes[j]] = parsed[j];

            return results;
        }

        if (inputs.Count == 1)
        {
            results[indexes[0]] = CleanUp(raw);
            return results;
        }

        Logger.Write($"Cloud AI batch response could not be parsed ({inputs.Count} items); translating one by one.");

        for (var j = 0; j < indexes.Count; j++)
        {
            token.ThrowIfCancellationRequested();
            var single = await SendAsync(SingleSystemPrompt(fromName, toName), inputs[j], false, token);
            results[indexes[j]] = CleanUp(single);
        }

        return results;
    }

    public static async Task<IReadOnlyList<string>> ListModelsAsync(
        HttpClient http,
        CloudAiPresetInfo preset,
        string key,
        string baseUrl,
        CancellationToken token = default)
    {
        var root = baseUrl.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(root))
            throw new TranslationException("The API address is empty.");

        if (string.IsNullOrWhiteSpace(key))
            throw new TranslationException("Enter the API key first.");

        string url;

        switch (preset.Protocol)
        {
            case CloudAiProtocol.Gemini:
                url = $"{root}/models?pageSize=200";
                break;
            case CloudAiProtocol.Anthropic:
                url = $"{root}/models?limit=100";
                break;
            default:
                url = $"{root}/models";
                break;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(request, preset.Protocol, key.Trim());

        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request, token);
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException($"Could not reach {new Uri(root).Host}. Check your internet connection.", ex);
        }
        catch (TaskCanceledException ex) when (!token.IsCancellationRequested)
        {
            throw new TranslationException("The request timed out.", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
                throw new TranslationException(DescribeError(response.StatusCode, body, preset.DisplayName));

            var node = JsonNode.Parse(body);
            var names = new List<string>();

            if (preset.Protocol == CloudAiProtocol.Gemini)
            {
                foreach (var item in node?["models"]?.AsArray() ?? new JsonArray())
                {
                    var methods = item?["supportedGenerationMethods"]?.AsArray();

                    if (methods is not null && !methods.Any(m => m?.GetValue<string>() == "generateContent"))
                        continue;

                    var name = item?["name"]?.GetValue<string>();

                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(NormalizeModel(name));
                }
            }
            else
            {
                foreach (var item in node?["data"]?.AsArray() ?? new JsonArray())
                {
                    var id = item?["id"]?.GetValue<string>();

                    if (!string.IsNullOrWhiteSpace(id))
                        names.Add(id);
                }
            }

            return names.Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    private string BatchSystemPrompt(string fromName, string toName)
    {
        var builder = new StringBuilder();
        builder.Append("You are a professional translation engine. ");
        builder.Append($"The user sends a JSON array of strings written in {fromName}. Translate every string into {toName}. ");
        builder.Append("Reply with ONLY a JSON array of strings containing exactly the same number of items in exactly the same order, with no explanation, no markdown and no code fences. ");
        builder.Append("The strings come from OCR of a screenshot, so they may contain recognition mistakes or stray line breaks; infer the intended meaning. ");
        builder.Append("Keep numbers, URLs, code, file names and brand names unchanged where translating them would be wrong.");

        if (_extraInstructions.Length > 0)
            builder.Append(' ').Append(_extraInstructions);

        return builder.ToString();
    }

    private string SingleSystemPrompt(string fromName, string toName)
    {
        var text = $"You are a professional translation engine. Translate the user's text from {fromName} to {toName}. " +
                   "Reply with ONLY the translation, with no quotes, no explanation and no commentary.";

        return _extraInstructions.Length > 0 ? $"{text} {_extraInstructions}" : text;
    }

    private async Task<string> SendAsync(string system, string user, bool expectJson, CancellationToken token)
    {
        var includeTemperature = true;

        for (var attempt = 1; ; attempt++)
        {
            token.ThrowIfCancellationRequested();

            using var request = BuildRequest(system, user, expectJson, includeTemperature);

            HttpResponseMessage response;

            try
            {
                response = await _http.SendAsync(request, token);
            }
            catch (HttpRequestException ex)
            {
                throw new TranslationException($"Could not reach {new Uri(_baseUrl).Host}. Check your internet connection.", ex);
            }
            catch (TaskCanceledException ex) when (!token.IsCancellationRequested)
            {
                throw new TranslationException("The AI service took too long to answer (timeout).", ex);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(token);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return ExtractText(body);
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentOutOfRangeException or FormatException)
                    {
                        throw new TranslationException("The AI service returned an unexpected response.", ex);
                    }
                }

                if (includeTemperature
                    && response.StatusCode == HttpStatusCode.BadRequest
                    && body.Contains("temperature", StringComparison.OrdinalIgnoreCase))
                {
                    includeTemperature = false;
                    attempt--;
                    continue;
                }

                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests
                                || response.StatusCode == HttpStatusCode.ServiceUnavailable
                                || response.StatusCode == HttpStatusCode.BadGateway
                                || response.StatusCode == HttpStatusCode.GatewayTimeout
                                || (int)response.StatusCode == 529;

                if (!retryable || attempt >= MaxAttempts)
                    throw new TranslationException(DescribeError(response.StatusCode, body, _preset.DisplayName));

                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 * attempt);

                if (delay > TimeSpan.FromSeconds(20))
                    throw new TranslationException(DescribeError(response.StatusCode, body, _preset.DisplayName));

                Logger.Write($"Cloud AI returned {(int)response.StatusCode}; retrying in {delay.TotalSeconds:0.#}s (attempt {attempt}).");
                await Task.Delay(delay, token);
            }
        }
    }

    private HttpRequestMessage BuildRequest(string system, string user, bool expectJson, bool includeTemperature)
    {
        string url;
        JsonObject payload;

        switch (_preset.Protocol)
        {
            case CloudAiProtocol.Gemini:
            {
                url = $"{_baseUrl}/models/{Uri.EscapeDataString(_model)}:generateContent";

                var generation = new JsonObject();

                if (includeTemperature)
                    generation["temperature"] = _temperature;

                if (expectJson)
                    generation["responseMimeType"] = "application/json";

                if (_model.Contains("2.5-flash", StringComparison.OrdinalIgnoreCase))
                    generation["thinkingConfig"] = new JsonObject { ["thinkingBudget"] = 0 };

                payload = new JsonObject
                {
                    ["systemInstruction"] = new JsonObject
                    {
                        ["parts"] = new JsonArray(new JsonObject { ["text"] = system })
                    },
                    ["contents"] = new JsonArray(new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray(new JsonObject { ["text"] = user })
                    }),
                    ["generationConfig"] = generation
                };

                break;
            }

            case CloudAiProtocol.Anthropic:
            {
                url = $"{_baseUrl}/messages";

                payload = new JsonObject
                {
                    ["model"] = _model,
                    ["max_tokens"] = 8192,
                    ["system"] = system,
                    ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = user })
                };

                if (includeTemperature)
                    payload["temperature"] = _temperature;

                break;
            }

            default:
            {
                url = $"{_baseUrl}/chat/completions";

                payload = new JsonObject
                {
                    ["model"] = _model,
                    ["messages"] = new JsonArray(
                        new JsonObject { ["role"] = "system", ["content"] = system },
                        new JsonObject { ["role"] = "user", ["content"] = user })
                };

                if (includeTemperature)
                    payload["temperature"] = _temperature;

                break;
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };

        ApplyAuth(request, _preset.Protocol, _key);
        return request;
    }

    private static void ApplyAuth(HttpRequestMessage request, CloudAiProtocol protocol, string key)
    {
        switch (protocol)
        {
            case CloudAiProtocol.Gemini:
                request.Headers.Add("x-goog-api-key", key);
                break;
            case CloudAiProtocol.Anthropic:
                request.Headers.Add("x-api-key", key);
                request.Headers.Add("anthropic-version", "2023-06-01");
                break;
            default:
                request.Headers.Add("Authorization", $"Bearer {key}");
                break;
        }
    }

    private string ExtractText(string body)
    {
        var node = JsonNode.Parse(body);

        switch (_preset.Protocol)
        {
            case CloudAiProtocol.Gemini:
            {
                var parts = node?["candidates"]?[0]?["content"]?["parts"]?.AsArray();

                if (parts is null)
                {
                    var reason = node?["promptFeedback"]?["blockReason"]?.GetValue<string>()
                                 ?? node?["candidates"]?[0]?["finishReason"]?.GetValue<string>()
                                 ?? "empty response";

                    throw new TranslationException($"Gemini returned no text ({reason}).");
                }

                var builder = new StringBuilder();

                foreach (var part in parts)
                {
                    if (part?["thought"]?.GetValue<bool>() == true)
                        continue;

                    builder.Append(part?["text"]?.GetValue<string>());
                }

                return builder.ToString();
            }

            case CloudAiProtocol.Anthropic:
            {
                var builder = new StringBuilder();

                foreach (var block in node?["content"]?.AsArray() ?? new JsonArray())
                {
                    if (block?["type"]?.GetValue<string>() == "text")
                        builder.Append(block["text"]?.GetValue<string>());
                }

                return builder.ToString();
            }

            default:
            {
                var content = node?["choices"]?[0]?["message"]?["content"];

                if (content is null)
                    throw new TranslationException("The AI service returned an empty response.");

                return content is JsonValue ? content.GetValue<string>() : content.ToString();
            }
        }
    }

    private static IReadOnlyList<string>? TryParseArray(string raw, int expected)
    {
        var text = StripFences(raw);

        try
        {
            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');

            JsonArray? array = null;

            if (start >= 0 && end > start)
            {
                array = JsonNode.Parse(text[start..(end + 1)]) as JsonArray;
            }
            else if (text.TrimStart().StartsWith('{') && JsonNode.Parse(text) is JsonObject obj)
            {
                array = obj.Select(x => x.Value).OfType<JsonArray>().FirstOrDefault();
            }

            if (array is null || array.Count != expected)
                return null;

            var result = new List<string>(expected);

            foreach (var item in array)
            {
                if (item is null)
                    return null;

                result.Add(item is JsonValue value && value.TryGetValue<string>(out var s) ? s : item.ToString());
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string StripFences(string text)
    {
        var trimmed = text.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstBreak = trimmed.IndexOf('\n');

        if (firstBreak < 0)
            return trimmed;

        trimmed = trimmed[(firstBreak + 1)..];

        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        return (lastFence >= 0 ? trimmed[..lastFence] : trimmed).Trim();
    }

    private static string CleanUp(string text)
    {
        var trimmed = StripFences(text);

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            trimmed = trimmed[1..^1];

        return trimmed;
    }

    private static string NormalizeModel(string model)
    {
        var trimmed = model.Trim();
        return trimmed.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? trimmed[7..] : trimmed;
    }

    private static string DescribeError(HttpStatusCode status, string body, string service)
    {
        var detail = ExtractErrorMessage(body);
        var code = (int)status;

        var prefix = status switch
        {
            HttpStatusCode.BadRequest => $"{service} rejected the request ({code}). Check the model name.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"{service} rejected the API key ({code}). Check that the key is correct and enabled.",
            HttpStatusCode.NotFound => $"{service} could not find that model or address ({code}). Check the model name.",
            HttpStatusCode.TooManyRequests => $"{service} says you hit the rate limit or free quota ({code}). Wait a bit and try again.",
            _ => $"{service} returned an error ({code})."
        };

        return string.IsNullOrWhiteSpace(detail) ? prefix : $"{prefix} {detail}";
    }

    private static string ExtractErrorMessage(string body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            var error = node?["error"];

            if (error is JsonValue)
                return error.GetValue<string>();

            var message = error?["message"]?.GetValue<string>();

            if (!string.IsNullOrWhiteSpace(message))
                return message.Length > 300 ? message[..300] : message;
        }
        catch (Exception)
        {
        }

        var flat = body.Trim();
        return flat.Length > 200 ? flat[..200] : flat;
    }
}
