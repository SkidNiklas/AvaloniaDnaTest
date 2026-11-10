using AvaloniaDnaTest.UI;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Styling;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--native-probe")) return NativeProbe.Run(args);
        using var ui = new EmbeddedUi(new SampleExcel());
        try
        {
            if (args.Contains("--smoke"))
            {
                // Same bootstrap and dispatcher as Excel, not a desktop-lifetime substitute.
                int closedCount = 0;
                ui.WindowClosed += (_, _) => closedCount++;
                for (int i = 0; i < 3; i++)
                {
                    await ui.ShowAsync(0, refresh: true, beforeActivation: () =>
                    {
                        if (!ui.IsWindowOpen) throw new Exception("Activation handoff must follow visible window.");
                        return Task.CompletedTask;
                    });
                    MainWindow? original = null;
                    await ui.InspectWindowAsync(window => original = window);
                    await ui.ShowAsync(0);
                    await ui.InspectWindowAsync(window =>
                    {
                        if (!ReferenceEquals(original, window) || !ui.IsWindowOpen)
                            throw new Exception("Activation must reuse the visible window.");
                    });
                    await Task.Delay(800);
                    await ui.InspectWindowAsync(window =>
                    {
                        var vm = (TestViewModel)window.DataContext!;
                        var input = window.FindControl<TextBox>("InputBox")!;
                        input.Text = "Bindningstest";
                        if (vm.Input != "Bindningstest") throw new Exception("TextBox to VM binding failed.");
                        vm.Input = "Hej från Avalonia";
                        if (input.Text != vm.Input) throw new Exception("VM to TextBox binding failed.");
                        window.FindControl<Slider>("ProgressSlider")!.Value = 65;
                        if (window.FindControl<ProgressBar>("TestProgressBar")!.Value != 65) throw new Exception("Slider/progress binding failed.");
                        if (window.FindControl<DataGrid>("TestGrid")!.ItemsSource.Cast<object>().Count() != 10) throw new Exception("DataGrid binding failed.");
                        vm.DarkTheme = false;
                        if (window.ActualThemeVariant != ThemeVariant.Light) throw new Exception("Light theme failed.");
                        vm.DarkTheme = true;
                        if (window.ActualThemeVariant != ThemeVariant.Dark) throw new Exception("Dark theme failed.");
                        if (!vm.ExcelInformation.Contains("Test.xlsx")) throw new Exception("Async service result did not reach UI.");
                        vm.Status = "Redo · bindningar, tema och asynkron informationshämtning verifierade";
                        if (i == 0 && args.Length > 1)
                        {
                            using var image = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
                            image.Render(window);
                            image.Save(args[1], PngBitmapEncoderOptions.Default);
                        }
                    });
                    await ui.CloseWindowAsync();
                    if (ui.IsWindowOpen || closedCount != i + 1)
                        throw new Exception("Window close state/event failed.");
                }
                // Closing during the asynchronous handoff must not activate a stale window.
                await ui.ShowAsync(0, beforeActivation: () => ui.CloseWindowAsync());
                if (ui.IsWindowOpen || closedCount != 4) throw new Exception("Close during activation handoff failed.");
                await ui.ShowAsync(0);
                await ui.CloseWindowAsync();
                Console.WriteLine("PASS: initialize once; show/close/reopen 3x; same-window activation; close during activation handoff and reopen; TextBox round-trip; Slider/ProgressBar; 10 DataGrid rows; light/dark; async service.");
                return 0;
            }
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ui.WindowClosed += (_, _) => closed.TrySetResult();
            await ui.ShowAsync(0, refresh: true);
            await closed.Task;
            return 0;
        }
        catch (Exception ex) { DiagnosticLog.Write(ex.ToString()); Console.Error.WriteLine(ex); return 1; }
    }
    private sealed class SampleExcel : IExcelInformationService
    {
        public Task<string> ReadAsync() => Task.FromResult("Separat testvärd (ingen Excel COM)\nExcel: simulerad · Bok: Test.xlsx · Blad: Blad1 · Cell: $A$1 · Värde: 42");
    }
}
