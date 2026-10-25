using FileMind.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileMind.WinUI.Views;

/// <summary>Devices matched to the mockup: device cards with storage meter,
/// capabilities, restriction badges, browse/organize actions — real DriveInfo.</summary>
public sealed partial class DevicesPage : Page
{
    public DevicesPage()
    {
        InitializeComponent();
        PcName.Text = Environment.MachineName;
        PcDetail.Text = $"{Environment.OSVersion.VersionString} · {Environment.UserName}";

        try
        {
            var sys = DriveInfo.GetDrives().FirstOrDefault(d =>
                d.IsReady && d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase));
            if (sys is not null)
            {
                var used = sys.TotalSize - sys.AvailableFreeSpace;
                var pct = Math.Max(0, Math.Min(100, 100.0 * used / sys.TotalSize));
                DriveMeter.SizeChanged += (_, _) => DriveMeterFill.Width = DriveMeter.ActualWidth * pct / 100;
                DriveSummary.Text = $"{DashboardPage.FormatBytes(used)} of {DashboardPage.FormatBytes(sys.TotalSize)} used";
            }
            var lines = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => $"{d.Name}  {(string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.DriveType.ToString() : d.VolumeLabel)}" +
                             $"  —  {DashboardPage.FormatBytes(d.AvailableFreeSpace)} free of {DashboardPage.FormatBytes(d.TotalSize)}")
                .ToList();
            DriveList.ItemsSource = lines;

            // removable drive cards, mockup-style
            var removable = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Removable)
                .Select(d => RemovableCard(d))
                .ToList();
            RemovableList.ItemsSource = removable;
        }
        catch { DriveSummary.Text = "Drive usage unavailable."; }
    }

    private static FmBentoCard RemovableCard(DriveInfo d)
    {
        var used = d.TotalSize - d.AvailableFreeSpace;
        var pct = Math.Max(0, Math.Min(100, 100.0 * used / d.TotalSize));
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(new TextBlock
        {
            Text = $"{d.Name} {(string.IsNullOrWhiteSpace(d.VolumeLabel) ? "Removable" : d.VolumeLabel)}",
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmTextH3"]
        });
        var meter = new Grid { Height = 8 };
        meter.Children.Add(new Microsoft.UI.Xaml.Controls.Border
        {
            CornerRadius = new CornerRadius(999),
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmSurfaceElevatedBrush"]
        });
        var fill = new Microsoft.UI.Xaml.Controls.Border
        {
            CornerRadius = new CornerRadius(999),
            Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmAccentBrush"],
            HorizontalAlignment = HorizontalAlignment.Left
        };
        meter.SizeChanged += (_, _) => fill.Width = meter.ActualWidth * pct / 100;
        meter.Children.Add(fill);
        body.Children.Add(meter);
        body.Children.Add(new TextBlock
        {
            Text = $"{DashboardPage.FormatBytes(used)} of {DashboardPage.FormatBytes(d.TotalSize)} used",
            FontSize = 12,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
        });
        var badge = new Microsoft.UI.Xaml.Controls.Border
        {
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmBadgeBorder"],
            Child = new TextBlock { Text = "Connected", FontSize = 11,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmStatusSuccessBrush"] }
        };
        var card = new FmBentoCard { IconGlyph = "\uE88E", Header = "USB drive", HeaderTrailing = badge, CardContent = body, MaxWidth = 420 };
        return card;
    }

    private void Browse_Click(object sender, RoutedEventArgs e) => GoTo("files");
    private void Organize_Click(object sender, RoutedEventArgs e) => GoTo("organizer");

    private void GoTo(string tag)
    {
        if (App.MainAppWindow?.Content is Shell shell) shell.NavigateTo(tag);
    }
}
