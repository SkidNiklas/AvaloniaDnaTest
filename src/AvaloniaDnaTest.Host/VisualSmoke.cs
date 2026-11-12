using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using AvaloniaDnaTest.UI;

internal static class VisualSmoke
{
    public static async Task RunAsync(EmbeddedUi ui)
    {
        await ui.ShowAsync(0, refresh: true);
        foreach (var size in new[] { (900, 600), (1100, 750), (1280, 900), (1280, 1100) })
        foreach (var dark in new[] { true, false })
        {
            await ui.InspectWindowAsync(w =>
            {
                w.Width = size.Item1; w.Height = size.Item2;
                w.ShowPage("ControlsPage");
                ((TestViewModel)w.DataContext!).DarkTheme = dark;
            });
            await Task.Delay(350);
            await ui.InspectWindowAsync(w =>
            {
                var scroll = w.FindControl<ScrollViewer>("ControlsPage")!;
                var bar = scroll.GetVisualDescendants().OfType<ScrollBar>().First(b => b.Orientation == Avalonia.Layout.Orientation.Vertical && b.TemplatedParent == scroll);
                var content = (Control)scroll.Content!;
                var contentRight = content.TranslatePoint(new Point(content.Bounds.Width, 0), scroll)!.Value.X;
                var barLeft = bar.TranslatePoint(default, scroll)!.Value.X;
                if (bar.IsVisible && contentRight > barLeft + 0.1) throw new Exception("Scrollbar overlaps content.");
                if (bar.IsVisible && Math.Abs(barLeft + bar.Bounds.Width - scroll.Bounds.Width) > 0.1) throw new Exception("Scrollbar not at right edge.");
                Console.WriteLine($"{size} {(dark ? "dark" : "light")}: viewport={scroll.Viewport}; extent={scroll.Extent}; contentRight={contentRight}; barLeft={barLeft}; barWidth={bar.Bounds.Width}; visible={bar.IsVisible}");
                using var image = new RenderTargetBitmap(new PixelSize((int)w.Bounds.Width, (int)w.Bounds.Height));
                image.Render(w);
                image.Save($"artifacts/visual-{size.Item1}x{size.Item2}-{(dark ? "dark" : "light")}.png", PngBitmapEncoderOptions.Default);
                scroll.Offset = new Vector(0, scroll.Extent.Height);
            });
            await Task.Delay(100);
            await ui.InspectWindowAsync(w =>
            {
                var scroll = w.FindControl<ScrollViewer>("ControlsPage")!;
                if (scroll.Extent.Height > scroll.Viewport.Height && scroll.Offset.Y <= 0) throw new Exception("Scrolling failed.");
                scroll.Offset = default;
            });
        }
        await ui.CloseWindowAsync();
        Console.WriteLine("PASS: eight theme/size layouts; scrollbar bounds; programmatic scrolling.");
    }
}
