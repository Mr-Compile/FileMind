using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace FileMind.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "FileMind";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppShell.TitleBarElement);

        // windows.md: resizable with a minimum usable size
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.PreferredMinimumWidth = 720;
            p.PreferredMinimumHeight = 480;
        }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 760));
    }
}
