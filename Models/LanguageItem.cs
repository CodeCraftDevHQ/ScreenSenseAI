namespace ScreenTranslator.Models;

public sealed class LanguageItem
{
    public string Code { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public bool IsRightToLeft { get; set; }

    public override string ToString() => $"{NativeName} ({EnglishName})";
}
