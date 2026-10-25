using FileMind.Application.Ports;
using FileMind.Application.Profiles;
using FileMind.Domain.Contracts;
using FileMind.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace FileMind.WinUI.Views;

/// <summary>Tools matched to the mockup: tool-grid on real services +
/// organizer rules list with toggles that persist via IRuleRepository.</summary>
public sealed partial class ToolsPage : Page
{
    private sealed record RuleRow(
        string Id, string Name, string Provenance, string PriorityText,
        string WhenThen, bool Enabled, FmOpState ChipState, Visibility StopVisibility)
    {
        public override string ToString() => $"{Name} — {(Enabled ? "enabled" : "off")} — {WhenThen}";
    }

    private IReadOnlyList<Rule> _rules = [];

    public ToolsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private void GoTo(string tag)
    {
        if (App.MainAppWindow?.Content is Shell shell) shell.NavigateTo(tag);
    }

    private void GoOrganizer(object s, PointerRoutedEventArgs e) => GoTo("organizer");
    private void GoActivity(object s, PointerRoutedEventArgs e) => GoTo("activity");

    private async Task LoadAsync()
    {
        var be = await App.Backend;
        var inbox = await ((ISmartInboxRepository)be.Store).GetOpenAsync();
        if (inbox.IsSuccess)
        {
            InboxDesc.Text = $"{inbox.Value!.Count} file(s) waiting for a human decision.";
            if (inbox.Value!.Count > 0)
            {
                InboxBadge.Visibility = Visibility.Visible;
                InboxBadgeText.Text = $"{inbox.Value!.Count} pending";
            }
        }

        var rules = await ((IRuleRepository)be.Store).GetAllAsync();
        _rules = rules.IsSuccess ? rules.Value! : [];
        RenderRules();
    }

    private void RenderRules()
    {
        RuleList.ItemsSource = _rules.Select(r => new RuleRow(
            r.Id, r.Name, r.Provenance.ToString(), $"priority {r.Priority}",
            $"when {HumanCond(r.ConditionRoot)} → {string.Join(", ", r.Actions.Select(HumanAct))}",
            r.Enabled,
            r.Enabled ? FmOpState.Completed : FmOpState.Offline,
            r.StopProcessing ? Visibility.Visible : Visibility.Collapsed)).ToList();
        RulesEmpty.Visibility = _rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Rule_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not string id) return;
        var rule = _rules.FirstOrDefault(r => r.Id == id);
        if (rule is null) return;
        var be = await App.Backend;
        var updated = rule with { Enabled = cb.IsChecked == true, UpdatedAt = DateTimeOffset.UtcNow };
        var res = await ((IRuleRepository)be.Store).UpsertAsync(updated);
        if (res.IsSuccess)
            _rules = _rules.Select(r => r.Id == id ? updated : r).ToList();
        else
            cb.IsChecked = !cb.IsChecked; // revert on failure
        RenderRules();
    }

    private static string HumanCond(ConditionGroup g) =>
        string.Join($" {g.Operator.ToString().ToUpperInvariant()} ",
            g.Conditions.Select(c => $"{c.Field} {c.Operator.ToString().ToLowerInvariant()} '{c.Value}'")
              .Concat(g.Groups.Select(HumanCond)));

    private static string HumanAct(RuleAction a) => a.Kind switch
    {
        RuleActionKind.RouteToCollection => $"route to collection '{a.Collection}'",
        RuleActionKind.Move => $"move to {a.DestinationTemplate}",
        RuleActionKind.Rename => $"rename to {a.NamingTemplate}",
        _ => a.Kind.ToString().ToLowerInvariant(),
    };

    private void ShowResult(string header, IEnumerable<(string Label, string Trailing)> rows)
    {
        ResultCard.Header = header;
        ResultPanel.Children.Clear();
        foreach (var (label, trailing) in rows)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis });
            var t = new TextBlock
            {
                Text = trailing, FontSize = 12,
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
            };
            Grid.SetColumn(t, 1); g.Children.Add(t);
            ResultPanel.Children.Add(g);
        }
        ResultCard.Visibility = Visibility.Visible;
    }

    private async void Analyze_Click(object sender, PointerRoutedEventArgs e)
    {
        var be = await App.Backend;
        var report = await be.Storage.AnalyzeAsync();
        if (!report.IsSuccess) { ShowResult("Storage analysis", [("Analysis failed", report.Error!.Message)]); return; }
        var r = report.Value!;
        ShowResult("Storage analysis",
            r.ByKind.OrderByDescending(b => b.Bytes)
                .Select(b => (b.Label, $"{DashboardPage.FormatBytes(b.Bytes)} · {b.Count:N0} file(s)")));
    }

    private async void Cleanup_Click(object sender, PointerRoutedEventArgs e)
    {
        var be = await App.Backend;
        var report = await be.Storage.AnalyzeAsync();
        if (!report.IsSuccess) { ShowResult("Cleanup", [("Analysis failed", report.Error!.Message)]); return; }
        var r = report.Value!;
        ShowResult("Largest indexed files",
            r.Largest.Take(10).Select(f =>
                (f.Path, $"{DashboardPage.FormatBytes(f.SizeBytes)} · {f.AgeDays}d old")));
    }

    private async void Dupes_Click(object sender, PointerRoutedEventArgs e)
    {
        ResultCard.Header = "Find duplicates";
        ResultPanel.Children.Clear();
        ResultPanel.Children.Add(new TextBlock
        {
            Text = "Hashing the index — this can take a while on large folders…",
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
        });
        ResultCard.Visibility = Visibility.Visible;
        var be = await App.Backend;
        var report = await be.Duplicates.FindAsync();
        if (!report.IsSuccess) { ShowResult("Find duplicates", [("Search failed", report.Error!.Message)]); return; }
        var groups = report.Value!.Groups;
        ShowResult("Find duplicates", groups.Count == 0
            ? [("No exact duplicates found.", "")]
            : groups.Take(15).Select(g =>
                ($"{g.Files.Count}× {System.IO.Path.GetFileName(g.Files[0].Path)}",
                 $"{DashboardPage.FormatBytes(g.ReclaimableBytes)} reclaimable" +
                 (g.Confirmed ? "" : " · unconfirmed"))));
    }

    private async void Export_Click(object sender, PointerRoutedEventArgs e)
    {
        var be = await App.Backend;
        var store = be.Store;
        var profile = new ProfileService(
            (IRuleRepository)store, (ICollectionRepository)store,
            (ISubjectRepository)store, (IProjectRepository)store,
            (IExclusionRepository)store, (ISettingsRepository)store,
            (IProfileRepository)store);
        var json = await profile.ExportAsync();
        if (!json.IsSuccess) { ShowResult("Export profile", [("Export failed", json.Error!.Message)]); return; }

        var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("JSON", [".json"]);
        picker.SuggestedFileName = "organizer-profile";
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await Windows.Storage.FileIO.WriteTextAsync(file, json.Value!);
        ShowResult("Export profile", [($"Profile written to {file.Path}", "done")]);
    }
}
