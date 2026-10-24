using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Controls;

public sealed partial class FmBentoCard : UserControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string),
            typeof(FmBentoCard), new PropertyMetadata("", (d, e) =>
                ((FmBentoCard)d).HeaderText.Text = (string)e.NewValue));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string),
            typeof(FmBentoCard), new PropertyMetadata("\uE8A5", (d, e) =>
                ((FmBentoCard)d).HeaderIcon.Glyph = (string)e.NewValue));

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public static readonly DependencyProperty CardContentProperty =
        DependencyProperty.Register(nameof(CardContent), typeof(object),
            typeof(FmBentoCard), new PropertyMetadata(null, (d, e) =>
                ((FmBentoCard)d).Body.Content = e.NewValue));

    public object CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public static readonly DependencyProperty FooterContentProperty =
        DependencyProperty.Register(nameof(FooterContent), typeof(object),
            typeof(FmBentoCard), new PropertyMetadata(null, (d, e) =>
                ((FmBentoCard)d).Footer.Content = e.NewValue));

    public object FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    public FmBentoCard() => InitializeComponent();
}
