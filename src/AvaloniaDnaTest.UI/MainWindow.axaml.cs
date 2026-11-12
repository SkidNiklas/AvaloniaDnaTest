using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
namespace AvaloniaDnaTest.UI;
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        ShowPage("OverviewPage");
        Closed += (_, _) => (this.FindControl<ContentControl>("HtmlPage")!.Content as HtmlPreview)?.Close();
    }
    public HtmlPreview OpenHtmlPreview()
    {
        var host = this.FindControl<ContentControl>("HtmlPage")!;
        host.Content ??= new HtmlPreview();
        ShowPage("HtmlPage");
        return (HtmlPreview)host.Content;
    }
    public void ShowPage(string name)
    {
        foreach (var button in this.GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("navigation")))
            button.Classes.Set("selected", button.Tag as string == name);
        foreach (string page in new[] { "OverviewPage", "ControlsPage", "HtmlPage", "SettingsPage" })
            this.FindControl<Control>(page)!.IsVisible = page == name;
    }
    private void NavigatePage(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string page) return;
        if (page == "HtmlPage") OpenHtmlPreview();
        else ShowPage(page);
    }
    private void ToggleMenu(object? sender, RoutedEventArgs e)
    {
        var shell = this.FindControl<SplitView>("NavigationShell")!;
        shell.IsPaneOpen = !shell.IsPaneOpen;
    }
}
