using System.Runtime.InteropServices;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AvaloniaDnaTest.UI;

/// <summary>One dispatcher/STA per loaded add-in, independent of Excel's message loop.</summary>
public sealed class EmbeddedUi : IDisposable
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _stop = new();
    private readonly IExcelInformationService _excel;
    private readonly Thread _thread;
    private MainWindow? _window;
    private volatile bool _disposed;
    public event EventHandler? WindowClosed;
    public EmbeddedUi(IExcelInformationService excel)
    {
        _excel = excel;
        _thread = new Thread(Run) { Name = "Avalonia Excel UI", IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }
    private void Run()
    {
        try
        {
            App.Builder().SetupWithoutStarting();
            // Signal only after the dispatcher has actually entered its message loop.
            Dispatcher.UIThread.Post(() => _ready.TrySetResult());
            Dispatcher.UIThread.MainLoop(_stop.Token);
        }
        catch (Exception ex) { _ready.TrySetException(ex); DiagnosticLog.Failure("Avalonia dispatcher", ex); }
    }
    public async Task ShowAsync(nint excelHwnd, bool refresh = false, bool about = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_disposed) return;
            if (_window is null)
            {
                _window = new MainWindow { DataContext = new TestViewModel(_excel), ShowInTaskbar = excelHwnd == 0 };
                _window.Closed += (_, _) => { _window = null; WindowClosed?.Invoke(this, EventArgs.Empty); };
                _window.Show();
                if (excelHwnd != 0)
                {
                    var hwnd = _window.TryGetPlatformHandle()!.Handle;
                    SetLastError(0);
                    if (SetWindowLongPtr(hwnd, -8 /* GWLP_HWNDPARENT: owner for top-level window */, excelHwnd) == 0 && Marshal.GetLastWin32Error() != 0)
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            _window.Activate();
            var vm = (TestViewModel)_window.DataContext!;
            if (refresh) vm.RefreshCommand.Execute(null);
            if (about) vm.AboutCommand.Execute(null);
        });
    }
    public async Task CloseWindowAsync()
    {
        await _ready.Task;
        await Dispatcher.UIThread.InvokeAsync(() => _window?.Close());
    }
    public async Task InspectWindowAsync(Action<MainWindow> inspect)
    {
        await _ready.Task;
        await Dispatcher.UIThread.InvokeAsync(() => inspect(_window ?? throw new InvalidOperationException("No open window.")));
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ready.Task.IsCompletedSuccessfully)
            Dispatcher.UIThread.Post(() =>
            {
                try { _window?.Close(); Dispatcher.UIThread.InvokeShutdown(); }
                finally { _stop.Cancel(); }
            });
        else _stop.Cancel();
        // No COM calls or synchronous Excel waits on this thread: bounded join is safe.
        if (!_thread.Join(TimeSpan.FromSeconds(3))) DiagnosticLog.Write("UI thread did not stop within 3 seconds.");
        else _stop.Dispose();
        // Avalonia global services remain initialized. Reuse the same add-in; restarting the
        // runtime inside this process is deliberately unsupported. Never call Environment.Exit.
    }
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("kernel32.dll")] private static extern void SetLastError(uint error);
}
