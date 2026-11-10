using System.Runtime.InteropServices;
using System.Diagnostics;
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
    private volatile bool _isWindowOpen;
    public bool IsWindowOpen => _isWindowOpen;
    public event EventHandler? WindowClosed;
    public EmbeddedUi(IExcelInformationService excel)
    {
        _excel = excel;
        _thread = new Thread(Run) { Name = "Avalonia Excel UI", IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        DiagnosticLog.Trace("Avalonia.STAThread.start requested");
        _thread.Start();
    }
    private void Run()
    {
        try
        {
            var initialization = Stopwatch.StartNew();
            DiagnosticLog.Trace("Avalonia.initialization.begin");
            App.Builder().SetupWithoutStarting();
            DiagnosticLog.Trace("Avalonia.initialization.setup complete");
            // Signal only after the dispatcher has actually entered its message loop.
            Dispatcher.UIThread.Post(() =>
            {
                DiagnosticLog.Write($"Avalonia initialization-to-dispatcher-ready={initialization.Elapsed.TotalMilliseconds:F2} ms (once per runtime)");
                _ready.TrySetResult();
                DiagnosticLog.Trace("Avalonia.dispatcher.ready");
            });
            Dispatcher.UIThread.MainLoop(_stop.Token);
        }
        catch (Exception ex) { _ready.TrySetException(ex); DiagnosticLog.Failure("Avalonia dispatcher", ex); }
    }
    public async Task ShowAsync(nint excelHwnd, bool refresh = false, bool about = false, Func<Task>? beforeActivation = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DiagnosticLog.Trace("Avalonia.ShowAsync.requested");
        await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        MainWindow? target = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_disposed) return;
            DiagnosticLog.Trace($"Avalonia.Show.dispatcher; existing={_window is not null}");
            if (_window is null)
            {
                _window = new MainWindow
                {
                    DataContext = new TestViewModel(_excel), ShowInTaskbar = excelHwnd == 0,
                    // Excel can update its Ribbon before this window requests focus.
                    ShowActivated = beforeActivation is null
                };
                _window.Opened += (_, _) => DiagnosticLog.Trace("Avalonia.Window.Opened event");
                _window.Activated += (_, _) => DiagnosticLog.Trace("Avalonia.Window.Activated event");
                _window.Deactivated += (_, _) => DiagnosticLog.Trace("Avalonia.Window.Deactivated event");
                _window.Closed += (_, _) => { DiagnosticLog.Trace("Avalonia.Window.Closed event"); _isWindowOpen = false; _window = null; WindowClosed?.Invoke(this, EventArgs.Empty); };
                DiagnosticLog.Trace("Avalonia.Window.Show.call (may activate)");
                _window.Show();
                DiagnosticLog.WindowShown();
                if (excelHwnd != 0)
                {
                    var hwnd = _window.TryGetPlatformHandle()!.Handle;
                    SetLastError(0);
                    if (SetWindowLongPtr(hwnd, -8 /* GWLP_HWNDPARENT: owner for top-level window */, excelHwnd) == 0 && Marshal.GetLastWin32Error() != 0)
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            target = _window;
            _isWindowOpen = _window.IsVisible;
        });
        if (target is null) return;
        if (beforeActivation is not null)
        {
            DiagnosticLog.Trace("Avalonia.beforeActivation.begin");
            await beforeActivation().ConfigureAwait(false);
            DiagnosticLog.Trace("Avalonia.beforeActivation.complete");
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // A user may close the visible window while the asynchronous handoff runs.
            if (_disposed || !ReferenceEquals(_window, target)) return;
            DiagnosticLog.Trace("Avalonia.Window.Activate.call");
            _window.Activate();
            DiagnosticLog.Trace("Avalonia.Window.Activate.return");
            if (_window.WindowState == Avalonia.Controls.WindowState.Minimized)
                _window.WindowState = Avalonia.Controls.WindowState.Normal;
            _isWindowOpen = _window.IsVisible;
            var vm = (TestViewModel)_window.DataContext!;
            if (refresh) vm.RefreshCommand.Execute(null);
            if (about) vm.AboutCommand.Execute(null);
        });
    }
    public async Task CloseWindowAsync()
    {
        DiagnosticLog.Trace("Avalonia.CloseWindowAsync.requested");
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
        DiagnosticLog.Trace("Avalonia.Dispose.requested");
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
