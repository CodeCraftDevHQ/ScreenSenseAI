using System.Net.Http;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Translation;

public sealed class TranslatorFactory
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly HttpClient _localLlmHttp = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly SettingsService _settings;

    public TranslatorFactory(SettingsService settings)
    {
        _settings = settings;
    }

    public ITranslator Create() => Create(_settings.Current.Provider);

    public ITranslator Create(TranslationProviderKind kind)
    {
        var s = _settings.Current;

        return kind switch
        {
            TranslationProviderKind.OfflineDictionary => new DictionaryTranslator(),
            TranslationProviderKind.Azure => new AzureTranslator(_http, s.AzureKey, s.AzureRegion),
            TranslationProviderKind.DeepL => new DeepLTranslator(_http, s.DeepLKey),
            TranslationProviderKind.LibreTranslate => new LibreTranslateTranslator(_http, s.LibreTranslateUrl, s.LibreTranslateKey),
            TranslationProviderKind.Ollama => new OllamaTranslator(_localLlmHttp, s.OllamaUrl, s.OllamaModel, s.OllamaTemperature, s.OllamaPromptTemplate),
            TranslationProviderKind.LmStudio => new LmStudioTranslator(_localLlmHttp, s.LmStudioUrl, s.LmStudioModel, s.LmStudioTemperature, s.LmStudioPromptTemplate),
            TranslationProviderKind.Offline => new OfflineTranslator(s.OfflineModelsPath),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
