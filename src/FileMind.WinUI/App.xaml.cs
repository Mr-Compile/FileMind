using FileMind.WinUI.Services;
using Microsoft.UI.Xaml;

namespace FileMind.WinUI;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    /// <summary>Shared backend composition root — opened once at launch;
    /// pages await it and surface the truthful empty/error states.</summary>
    public static Task<FmBackend> Backend { get; } = FmBackend.OpenAsync();
    public static FmThemeService Theme { get; } = new();

    /// <summary>Main window handle for HWND-needing APIs (pickers).</summary>
    public static Window? MainAppWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "filemind-winui-crash.txt"),
                    $"{e.Exception}");
            }
            catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainAppWindow = _window;
        _window.Activate();
    }
}
