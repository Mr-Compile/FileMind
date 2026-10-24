using FileMind.WinUI.Icons;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileMind.WinUI.Controls;

/// <summary>GUIDE component state contract.</summary>
public enum FmComponentState { Idle, Loading, Success, Empty, Warning, Error }

public sealed partial class FmStateView : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(FmComponentState),
            typeof(FmStateView), new PropertyMetadata(FmComponentState.Idle, OnChanged));

    public FmComponentState State
    {
        get => (FmComponentState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string),
            typeof(FmStateView), new PropertyMetadata("", OnChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(string),
            typeof(FmStateView), new PropertyMetadata("", OnChanged));

    public string Body
    {
        get => (string)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public static readonly DependencyProperty ActionLabelProperty =
        DependencyProperty.Register(nameof(ActionLabel), typeof(string),
            typeof(FmStateView), new PropertyMetadata(null, OnChanged));

    public string? ActionLabel
    {
        get => (string?)GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    public event RoutedEventHandler? ActionClick;

    public FmStateView()
    {
        InitializeComponent();
        StateAction.Click += (_, e) => ActionClick?.Invoke(this, e);
        Render();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((FmStateView)d).Render();

    private void Render()
    {
        var (glyph, brushKey, ring) = State switch
        {
            FmComponentState.Loading => ("", null, true),
            FmComponentState.Success => (((FontIcon)FmIcons.Element(FmIcon.Success)).Glyph, "StSuccess", false),
            FmComponentState.Empty => (((FontIcon)FmIcons.Element(FmIcon.Folder)).Glyph, "StNeutral", false),
            FmComponentState.Warning => (((FontIcon)FmIcons.Element(FmIcon.Warning)).Glyph, "StWarning", false),
            FmComponentState.Error => (((FontIcon)FmIcons.Element(FmIcon.Error)).Glyph, "StError", false),
            _ => (((FontIcon)FmIcons.Element(FmIcon.Info)).Glyph, "StInfo", false),
        };
        StateRing.IsActive = ring;
        StateRing.Visibility = ring ? Visibility.Visible : Visibility.Collapsed;
        StateIcon.Visibility = ring ? Visibility.Collapsed : Visibility.Visible;
        StateIcon.Glyph = glyph;
        StateIcon.Foreground = brushKey is null
            ? (Brush)Resources["StNeutral"] : (Brush)Resources[brushKey];
        StateTitle.Text = Title;
        StateBody.Text = Body;
        StateAction.Content = ActionLabel;
        StateAction.Visibility = ActionLabel is { Length: > 0 }
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
