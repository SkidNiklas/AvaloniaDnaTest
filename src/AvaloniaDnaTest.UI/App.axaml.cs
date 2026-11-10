using Avalonia;
using Avalonia.Markup.Xaml;

namespace AvaloniaDnaTest.UI;
public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public static AppBuilder Builder() => AppBuilder.Configure<App>().UseWin32().UseSkia().UseHarfBuzz();
}
