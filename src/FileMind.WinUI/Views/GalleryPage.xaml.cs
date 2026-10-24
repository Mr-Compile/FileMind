using FileMind.Domain.Errors;
using FileMind.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

public sealed partial class GalleryPage : Page
{
    public GalleryPage()
    {
        InitializeComponent();
        RenderFriendlyError();
    }

    private void RenderFriendlyError()
    {
        var err = FmErrorPresenter.Present(
            new FileMindError(ErrorCode.InsufficientSpace, "ENOSPC: no space left on device", false, false));
        ErrorSample.Children.Add(new TextBlock
        {
            Text = err.Title, Style = (Style)Application.Current.Resources["FmTextH3"]
        });
        ErrorSample.Children.Add(new TextBlock
        {
            Text = err.Explanation, TextWrapping = TextWrapping.Wrap
        });
        var palette = new FmThemeService().ResolvedPalette();
        var secondary = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(255,
                Convert.ToByte(palette.TextSecondary[1..3], 16),
                Convert.ToByte(palette.TextSecondary[3..5], 16),
                Convert.ToByte(palette.TextSecondary[5..7], 16)));
        ErrorSample.Children.Add(new TextBlock
        {
            Text = err.Recovery, TextWrapping = TextWrapping.Wrap,
            Foreground = secondary
        });
        ErrorSample.Children.Add(new Expander
        {
            Header = "Technical details",
            Content = new TextBlock { Text = err.TechnicalDetails, Style = (Style)Application.Current.Resources["FmTextMono"] }
        });
    }
}
