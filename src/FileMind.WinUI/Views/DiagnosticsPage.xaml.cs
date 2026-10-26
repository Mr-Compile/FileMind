using FileMind.Application.Ports;
using FileMind.Intelligence.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

namespace FileMind.WinUI.Views;

/// <summary>
/// Diagnostics (crossplatform-ux/04_HELP_ABOUT), mirrored from the Android
/// Diagnostics screen: version/device/storage/grants visible, paths only on
/// opt-in, one-tap copy for support.
/// </summary>
public sealed partial class DiagnosticsPage : Page
{
    private IReadOnlyList<string> _watched = [];
    private string _dbPath = "";
    private string _version = "dev";

    public DiagnosticsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var be = await App.Backend;

        _version = global::System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                typeof(App).Assembly)?.InformationalVersion
            ?? typeof(App).Assembly.GetName().Version?.ToString() ?? "dev";
        VerText.Text = _version;

        DeviceText.Text = $"{Environment.MachineName} · {Environment.ProcessorCount}-core " +
            $"{(Environment.Is64BitProcess ? "x64" : "x86")}";
        OsText.Text = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

        try
        {
            var drive = new System.IO.DriveInfo("C");
            StorageText.Text =
                $"{DashboardPage.FormatBytes(drive.AvailableFreeSpace)} free of " +
                $"{DashboardPage.FormatBytes(drive.TotalSize)}";
        }
        catch { StorageText.Text = "unavailable"; }

        var report = await be.Storage.AnalyzeAsync();
        IndexText.Text = report.IsSuccess
            ? $"{report.Value!.FileCount:N0} file(s)"
            : "index unavailable";

        var watched = await ((IWatchedFolderRepository)be.Store).GetAllAsync();
        _watched = watched.IsSuccess ? watched.Value! : [];
        WatchedText.Text = $"{_watched.Count} folder(s)";

        _dbPath = Environment.GetEnvironmentVariable("FILEMIND_DB")
            ?? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileMind", "filemind.db");

        var ai = GroqOptions.Load((ISettingsRepository)be.Store, null);
        AiText.Text = ai.Enabled ? "Groq — enabled (opt-in)" : "Groq — off by default";
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var includePaths = PathsToggle.IsOn;
        var info = new System.Text.StringBuilder()
            .AppendLine($"FileMind {_version}")
            .AppendLine($"Device: {DeviceText.Text}")
            .AppendLine($"OS: {OsText.Text}")
            .AppendLine($"Storage: {StorageText.Text}")
            .AppendLine($"Indexed: {IndexText.Text}")
            .AppendLine($"Watched folders: {WatchedText.Text}")
            .AppendLine($"AI: {AiText.Text}");
        if (includePaths)
        {
            info.AppendLine($"db: {_dbPath}");
            foreach (var w in _watched) info.AppendLine($"watch: {w}");
        }

        var pkg = new DataPackage();
        pkg.SetText(info.ToString());
        Clipboard.SetContent(pkg);

        CopiedNote.Visibility = Visibility.Visible;
        CopiedNote.Text = includePaths
            ? "Copied — includes paths you opted in"
            : "Copied to clipboard";
    }
}
