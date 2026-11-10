using Avalonia.Controls;
using Avalonia.Markup.Xaml;
namespace AvaloniaDnaTest.UI;
public sealed partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}
