using FileMind.Application.Decisions;
using FileMind.Application.Ports;
using FileMind.Branding;
using FileMind.Domain.Contracts;
using FileMind.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

/// <summary>Settings — shared IA sections (crossplatform-ux SHARED_UX_SYSTEM),
/// same section set as the Android SettingsScreen. Theme/density apply live;
/// conflict policy, notification prefs and organization mode persist via
/// ISettingsRepository (organization.mode is the same key Android writes).</summary>
public sealed partial class SettingsPage : Page
{
    private const string ConflictKey = "ui.conflictPolicy";
    private const string NotifDoneKey = "ui.notify.operationDone";
    private const string NotifWarnKey = "ui.notify.warnings";
    private const string NotifInboxKey = "ui.notify.smartInbox";
    public const string DefaultFolderKey = "ui.defaultFolder";

    private bool _ready;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private void GoTo(string tag)
    {
        if (App.MainAppWindow?.Content is Shell shell) shell.NavigateTo(tag);
    }

    private void GoTools(object sender, RoutedEventArgs e) => GoTo("tools");
    private void GoDevices(object sender, RoutedEventArgs e) => GoTo("devices");
    private void GoDiagnostics(object sender, RoutedEventArgs e) => GoTo("help/diagnostics");
    private void GoHelp(object sender, RoutedEventArgs e) => GoTo("help");
    private void GoAbout(object sender, RoutedEventArgs e) => GoTo("help/about");

    private async Task LoadAsync()
    {
        var be = await App.Backend;
        var settings = (ISettingsRepository)be.Store;

        ThemeSystem.IsChecked = App.Theme.Mode == ThemeMode.System;
        ThemeLight.IsChecked = App.Theme.Mode == ThemeMode.Light;
        ThemeDark.IsChecked = App.Theme.Mode == ThemeMode.Dark;
        DensityCheck.IsChecked = App.Theme.Density == FmDensity.Compact;
        MotionNote.Text = App.Theme.ReducedMotion
            ? "Reduced motion is on (Windows “Show animations” off) — non-essential motion is skipped."
            : "";

        var conflict = (await settings.GetAsync(ConflictKey)).Value ?? "KeepBoth";
        ConflictPicker.SelectedItem = ConflictPicker.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(o => (string)o.Tag == conflict)
            ?? ConflictPicker.Items[0];

        ModePicker.SelectedItem = ModePicker.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(o => (string)o.Tag == be.ModePolicy.Mode.ToString())
            ?? ModePicker.Items[0];

        var watched = await ((IWatchedFolderRepository)be.Store).GetAllAsync();
        if (watched.IsSuccess && watched.Value!.Count > 0)
        {
            WatchedList.ItemsSource = watched.Value;
            WatchedEmpty.Visibility = Visibility.Collapsed;
        }

        NotifDone.IsChecked = (await settings.GetAsync(NotifDoneKey)).Value != "false";
        NotifWarn.IsChecked = (await settings.GetAsync(NotifWarnKey)).Value != "false";
        NotifInbox.IsChecked = (await settings.GetAsync(NotifInboxKey)).Value != "false";

        var dbPath = Environment.GetEnvironmentVariable("FILEMIND_DB")
            ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileMind", "filemind.db");
        DbPath.Text = dbPath;

        var ai = FileMind.Intelligence.AI.GroqOptions.Load(settings, null);
        AiStatus.Text = ai.Enabled
            ? "Groq — enabled (opt-in). Requests pass the privacy gate and quota."
            : "Groq — off by default. Enable and set a key via the host ai-key verb.";

        DefaultFolderBox.Text = (await settings.GetAsync(DefaultFolderKey)).Value ?? "";

        var ver = global::System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                typeof(App).Assembly)?.InformationalVersion
            ?? typeof(App).Assembly.GetName().Version?.ToString() ?? "dev";
        VersionText.Text = ver;
        AboutLinkText.Text = $"About FileMind — Version {ver} · offline-first file organizer";

        _ready = true;
    }

    private async void DefaultFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        DefaultFolderBox.Text = folder.Path;
        var be = await App.Backend;
        await ((ISettingsRepository)be.Store).SetAsync(DefaultFolderKey, folder.Path);
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var mode = ThemeDark.IsChecked == true ? ThemeMode.Dark
            : ThemeLight.IsChecked == true ? ThemeMode.Light
            : ThemeMode.System;
        if (App.MainAppWindow?.Content is FrameworkElement root)
            App.Theme.ApplyTheme(root, mode);
    }

    private void Density_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        App.Theme.SetDensity(DensityCheck.IsChecked == true
            ? FmDensity.Compact : FmDensity.Comfortable);
    }

    private async void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || ModePicker.SelectedItem is not ComboBoxItem item) return;
        if (!Enum.TryParse<OrganizationMode>((string)item.Tag, out var mode)) return;
        var be = await App.Backend;
        // Same canonical storage form the Android app writes (ModePolicy.display).
        await ((ISettingsRepository)be.Store).SetAsync(
            ModePolicy.SettingsKey, ModePolicy.Display(mode));
        be.SetMode(mode);
    }

    private async void Conflict_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || ConflictPicker.SelectedItem is not ComboBoxItem item) return;
        var be = await App.Backend;
        await ((ISettingsRepository)be.Store).SetAsync(ConflictKey, (string)item.Tag);
    }

    private async void Notif_Clicked(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not CheckBox cb) return;
        var key = cb == NotifDone ? NotifDoneKey : cb == NotifWarn ? NotifWarnKey : NotifInboxKey;
        var be = await App.Backend;
        await ((ISettingsRepository)be.Store).SetAsync(key, cb.IsChecked == true ? "true" : "false");
    }
}
