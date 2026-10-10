namespace ScreenTranslator.Services.Translation;

public enum CloudAiProtocol
{
    Gemini,
    OpenAiCompatible,
    Anthropic
}

public sealed record CloudAiPresetInfo(
    string Id,
    string DisplayName,
    CloudAiProtocol Protocol,
    string DefaultBaseUrl,
    string DefaultModel,
    string KeyHelp);

public static class CloudAiPresets
{
    public static readonly IReadOnlyList<CloudAiPresetInfo> All = new[]
    {
        new CloudAiPresetInfo("Gemini", "Google Gemini", CloudAiProtocol.Gemini,
            "https://generativelanguage.googleapis.com/v1beta", "gemini-2.5-flash",
            "aistudio.google.com/apikey"),
        new CloudAiPresetInfo("OpenAI", "OpenAI (ChatGPT)", CloudAiProtocol.OpenAiCompatible,
            "https://api.openai.com/v1", "gpt-4o-mini",
            "platform.openai.com/api-keys"),
        new CloudAiPresetInfo("Anthropic", "Anthropic (Claude)", CloudAiProtocol.Anthropic,
            "https://api.anthropic.com/v1", "claude-haiku-5-5",
            "console.anthropic.com"),
        new CloudAiPresetInfo("OpenRouter", "OpenRouter", CloudAiProtocol.OpenAiCompatible,
            "https://openrouter.ai/api/v1", "openrouter/auto",
            "openrouter.ai/keys"),
        new CloudAiPresetInfo("Groq", "Groq", CloudAiProtocol.OpenAiCompatible,
            "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile",
            "console.groq.com/keys"),
        new CloudAiPresetInfo("DeepSeek", "DeepSeek", CloudAiProtocol.OpenAiCompatible,
            "https://api.deepseek.com/v1", "deepseek-chat",
            "platform.deepseek.com/api_keys"),
        new CloudAiPresetInfo("Mistral", "Mistral", CloudAiProtocol.OpenAiCompatible,
            "https://api.mistral.ai/v1", "mistral-small-latest",
            "console.mistral.ai/api-keys"),
        new CloudAiPresetInfo("Custom", "Custom (any OpenAI-compatible API)", CloudAiProtocol.OpenAiCompatible,
            string.Empty, string.Empty,
            string.Empty)
    };

    public static CloudAiPresetInfo Get(string? id) =>
        All.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
