using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

/// <summary>Devices — real local drives via DriveInfo; Android truthful
/// "Planned" per PLAN-014 (PLAN-010 owns Android).</summary>
public sealed partial class DevicesPage : Page
{
    public DevicesPage()
    {
        InitializeComponent();
        PcName.Text = Environment.MachineName;
        PcDetail.Text = $"{Environment.OSVersion.VersionString} · {Environment.UserName}";
        DriveList.ItemsSource = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => $"{d.Name}  {d.VolumeLabel}  —  {DashboardPage.FormatBytes(d.AvailableFreeSpace)} free of {DashboardPage.FormatBytes(d.TotalSize)}")
            .ToList();
    }
}
