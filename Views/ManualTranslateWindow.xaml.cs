using System.Windows;
using System.Windows.Controls;
using ScreenTranslator.Localization;
using ScreenTranslator.Models;
using ScreenTranslator.Services;
using ScreenTranslator.Services.Text;
using ScreenTranslator.Services.Translation;

namespace ScreenTranslator.Views;

public partial class ManualTranslateWindow : Window
{
    private readonly SettingsService _settings;
    private readonly TranslatorFactory _translatorFactory;

    public ManualTranslateWindow(SettingsService settings, TranslatorFactory translatorFactory)
    {
        _settings = settings;
        _translatorFactory = translatorFactory;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        foreach (var language in LanguageCatalog.All)
            TargetLanguageBox.Items.Add(language);

        TargetLanguageBox.SelectedItem = LanguageCatalog.Find(_settings.Current.TargetLanguage)
            ?? TargetLanguageBox.Items[0];
    }

    private async void OnTranslateClick(object sender, RoutedEventArgs e)
    {
        var text = InputTextBox.Text.Trim();

        if (string.IsNullOrEmpty(text))
            return;

        var target = (TargetLanguageBox.SelectedItem as LanguageItem)?.Code ?? _settings.Current.TargetLanguage;
        var source = LanguageDetector.Detect(text, _settings.Current.SourceLanguage);

        TranslateButton.IsEnabled = false;
        ResultTextBox.Text = "...";

        try
        {
            var translator = _translatorFactory.Create();

            if (!translator.IsConfigured)
                throw new TranslationException("ProviderNotConfigured");

            var result = await translator.TranslateAsync(new[] { text }, source, target);

            ResultTextBox.Text = result.FirstOrDefault() ?? string.Empty;
            ResultTextBox.FlowDirection = LanguageCatalog.IsRightToLeft(target)
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
        }
        catch (NotSupportedException ex)
        {
            var message = Loc.Instance.Language == "fa"
                ? "این گزینه هنوز پیاده‌سازی نشده. از تنظیمات، سرویس ترجمه را روی گزینهٔ دیگری بگذار.\n\n" + ex.Message
                : "That option isn't implemented yet. Pick a different translation provider in Settings.\n\n" + ex.Message;

            ResultTextBox.Text = string.Empty;
            MessageBox.Show(message, Loc.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (TranslationException ex)
        {
            var message = ex.Message == "ProviderNotConfigured" ? Loc.Instance["ProviderNotConfigured"] : ex.Message;

            ResultTextBox.Text = string.Empty;
            MessageBox.Show(message, Loc.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ResultTextBox.Text = string.Empty;
            MessageBox.Show(ex.Message, Loc.Instance["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            TranslateButton.IsEnabled = true;
        }
    }

    private void OnCopyResult(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ResultTextBox.Text);
        }
        catch (Exception)
        {
        }
    }
}
