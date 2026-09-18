using System.Windows;
using ScreenTranslator.Localization;
using ScreenTranslator.Services;

namespace ScreenTranslator.Views;

public partial class TextViewWindow : Window
{
    public TextViewWindow(IReadOnlyList<OverlayItem> items)
    {
        InitializeComponent();

        if (items.Count == 0)
        {
            OriginalTextBox.Text = Loc.Instance["NoTranslationYet"];
            TranslatedTextBox.Text = Loc.Instance["NoTranslationYet"];
            return;
        }

        OriginalTextBox.Text = string.Join(Environment.NewLine, items.Select(x => x.OriginalText));
        TranslatedTextBox.Text = string.Join(Environment.NewLine, items.Select(x => x.TranslatedText));

        if (items[0].IsRightToLeft)
            TranslatedTextBox.FlowDirection = FlowDirection.RightToLeft;
    }

    private void OnCopyOriginal(object sender, RoutedEventArgs e) => TrySetClipboard(OriginalTextBox.Text);

    private void OnCopyTranslated(object sender, RoutedEventArgs e) => TrySetClipboard(TranslatedTextBox.Text);

    private static void TrySetClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception)
        {
        }
    }
}
