using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using ExcelDna.Integration;
using ExcelDna.Integration.CustomUI;
using AvaloniaDnaTest.UI;

namespace AvaloniaDnaTest.AddIn;

public sealed class AddIn : IExcelAddIn
{
    internal static EmbeddedUi? Ui;
    internal static ExcelInformationService? Excel;
    internal static volatile bool Stopping;
    internal static int ExcelThreadId;
    public void AutoOpen()
    {
        Stopping = false;
        ExcelThreadId = Environment.CurrentManagedThreadId;
        DiagnosticLog.ExcelWindow = ExcelDnaUtil.WindowHandle;
        DiagnosticLog.Trace("AddIn.AutoOpen");
        Excel = new ExcelInformationService();
        DiagnosticLog.XllPath = ExcelDnaUtil.XllPath;
        DiagnosticLog.Snapshot("AutoOpen");
        DiagnosticLog.Write($"AutoOpen: Excel thread {Environment.CurrentManagedThreadId}, x64={Environment.Is64BitProcess}");
    }
    public void AutoClose()
    {
        Stopping = true;
        Ribbon.Current?.Stop();
        Excel?.Stop();
        Ui?.Dispose();
        DiagnosticLog.Trace("AddIn.AutoClose complete");
        DiagnosticLog.FlushTrace();
        DiagnosticLog.Write("AutoClose complete. Restart Excel before reloading an unloaded add-in.");
    }
}

[ComVisible(true)]
public sealed class Ribbon : ExcelRibbon
{
    private enum State { Ready, Loading, Open }
    internal static Ribbon? Current;
    private IRibbonUI? _ribbon;
    private State _state;
    private bool _busy;
    private int _request;
    private double? _coldVisibleMs;
    private long _transitionTimestamp;
    private long _invalidateTimestamp;
    private int _transitionId;
    private static int _postId;
    // Detached bitmaps remain alive while Office may cache the image objects.
    private readonly Dictionary<State, Bitmap> _images = new();
    public override string GetCustomUI(string ribbonId) => """
        <customUI xmlns="http://schemas.microsoft.com/office/2009/07/customui" onLoad="OnLoad">
          <ribbon><tabs><tab id="AvaloniaTestTab" label="Avalonia Test"><group id="AvaloniaTestGroup" label="Testverktyg">
            <button id="OpenTest" getLabel="GetLabel" getImage="GetImage" getEnabled="GetEnabled" size="large" onAction="OpenWindow"/>
            <button id="ExcelInfo" label="Excel-information" imageMso="Info" onAction="ExcelInformation"/>
            <button id="AboutTest" label="Om" imageMso="Help" onAction="About"/>
          </group></tab></tabs></ribbon>
        </customUI>
        """;
    public void OnLoad(IRibbonUI ribbon) { Current = this; _ribbon = ribbon; Trace("OnLoad"); }
    private void Trace(string context) => DiagnosticLog.Trace($"Ribbon.{context}; state={_state}; request={_request}; transition={_transitionId}; expectedExcelThread={AddIn.ExcelThreadId}; onExcelThread={Environment.CurrentManagedThreadId == AddIn.ExcelThreadId}");
    private void Callback(string name) => Trace($"callback.{name}; since-state-ms={DiagnosticLog.MillisecondsSince(_transitionTimestamp)}; since-invalidate-ms={DiagnosticLog.MillisecondsSince(_invalidateTimestamp)}; since-window-shown-ms={DiagnosticLog.MillisecondsSince(DiagnosticLog.WindowShownTimestamp)}");
    public string GetLabel(IRibbonControl control)
    {
        Callback("getLabel");
        return _state switch { State.Loading => "Startar...", State.Open => "Visa testfönster", _ => "Öppna testfönster" };
    }
    public bool GetEnabled(IRibbonControl control)
    {
        Callback("getEnabled");
        return _state != State.Loading && !AddIn.Stopping;
    }
    public object GetImage(IRibbonControl control)
    {
        Callback("getImage");
        if (!_images.TryGetValue(_state, out var image))
        {
            using var stream = typeof(Ribbon).Assembly.GetManifestResourceStream($"AvaloniaDnaTest.AddIn.Icons.{_state}.png")
                ?? throw new InvalidOperationException("Ribbon icon missing: " + _state);
            using var source = new Bitmap(stream);
            image = new Bitmap(source);
            _images.Add(_state, image);
        }
        return image;
    }
    private void Transition(State next, string reason)
    {
        if (Environment.CurrentManagedThreadId != AddIn.ExcelThreadId)
        {
            DiagnosticLog.Trace($"Ribbon.thread-guard rerouting transition to {next}; {reason}");
            Post(() => Transition(next, reason), "thread-guard");
            return;
        }
        var previous = _state;
        _state = next;
        _transitionTimestamp = Stopwatch.GetTimestamp();
        ++_transitionId;
        Trace($"state {previous}->{next}; {reason}");
        Trace("InvalidateControl.requested");
        try
        {
            if (_ribbon is null) { Trace("InvalidateControl.skipped (no onLoad reference)"); return; }
            _invalidateTimestamp = Stopwatch.GetTimestamp();
            Trace($"InvalidateControl.call; since-state-ms={DiagnosticLog.MillisecondsSince(_transitionTimestamp)}");
            _ribbon.InvalidateControl("OpenTest");
            Trace("InvalidateControl.return (not proof of paint)");
        }
        catch (Exception ex) { DiagnosticLog.Failure("Ribbon invalidation", ex); }
    }
    public void OpenWindow(IRibbonControl control) => Request();
    public void ExcelInformation(IRibbonControl control) => Request(refresh: true);
    public void About(IRibbonControl control) => Request(about: true);
    private async void Request(bool refresh = false, bool about = false)
    {
        Trace($"onAction; refresh={refresh}; about={about}; busy={_busy}");
        if (_busy || AddIn.Stopping) return;
        var clock = Stopwatch.StartNew();
        _busy = true;
        int request = ++_request;
        bool cold = AddIn.Ui is null;
        bool activation = AddIn.Ui?.IsWindowOpen == true;
        Transition(State.Loading, $"request={request}; kind={(cold ? "cold" : activation ? "activate" : "reopen")}");
        DiagnosticLog.Write($"Startup request={request}; click-to-loading={clock.Elapsed.TotalMilliseconds:F2} ms");
        try
        {
            // Return from the Ribbon callback. This asynchronous grace period permits
            // Excel to service callbacks/paint; it is NOT a paint-completion barrier.
            await Task.Delay(120).ConfigureAwait(false);
            if (AddIn.Stopping) return;
            Post(() => Start(request, clock, cold, activation, refresh, about), "start after 120ms grace");
        }
        catch (Exception ex) { PostFailure(request, ex); }
    }
    private async void Start(int request, Stopwatch clock, bool cold, bool activation, bool refresh, bool about)
    {
        if (AddIn.Stopping || request != _request) return;
        try
        {
            Trace("Start.enter");
            var hwnd = ExcelDnaUtil.WindowHandle;
            DiagnosticLog.ExcelWindow = hwnd;
            PackedNativeLibraries.Configure();
            if (AddIn.Ui is null)
            {
                AddIn.Ui = new EmbeddedUi(AddIn.Excel ?? throw new InvalidOperationException("Add-in is not open."));
                AddIn.Ui.WindowClosed += (_, _) => Post(() =>
                {
                    if (AddIn.Ui?.IsWindowOpen != true) Transition(State.Ready, "window closed");
                }, "WindowClosed -> Ready");
            }
            await AddIn.Ui.ShowAsync(hwnd, refresh, about, () => BeforeActivation(request)).ConfigureAwait(false);
            DiagnosticLog.Trace($"Ribbon.ShowAsync.completed; request={request}; since-window-shown-ms={DiagnosticLog.MillisecondsSince(DiagnosticLog.WindowShownTimestamp)}");
            double visibleMs = clock.Elapsed.TotalMilliseconds;
            Post(() =>
            {
                if (request != _request) return;
                _busy = false;
                Transition(AddIn.Ui?.IsWindowOpen == true ? State.Open : State.Ready, "show completed / current visibility");
                if (cold) _coldVisibleMs = visibleMs;
                DiagnosticLog.Write($"Startup request={request}; kind={(cold ? "cold" : activation ? "activate" : "reopen")}; click-to-visible={visibleMs:F2} ms; cold-minus-current={(_coldVisibleMs - visibleMs)?.ToString("F2") ?? "n/a"} ms (includes scheduling grace)");
            }, "ShowAsync completed -> Open/Ready");
            DiagnosticLog.Snapshot("Ribbon window opened");
        }
        catch (Exception ex) { PostFailure(request, ex); }
    }
    private async Task BeforeActivation(int request)
    {
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            if (request == _request && AddIn.Ui?.IsWindowOpen == true)
                Transition(State.Open, "window visible; before explicit activation");
            updated.TrySetResult();
        }, "visible -> Open before activation");
        try
        {
            // Bound the wait: Excel may postpone macros while busy. Never hold the
            // Avalonia dispatcher or block either STA waiting for the other thread.
            await updated.Task.WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            await Task.Delay(120).ConfigureAwait(false);
            DiagnosticLog.Trace("Ribbon.beforeActivation.grace complete (not proof of paint)");
        }
        catch (TimeoutException)
        {
            DiagnosticLog.Trace("Ribbon.beforeActivation.timeout; activate normally; queued update may run later");
        }
    }
    private static void Post(Action action, string context = "failure -> Ready")
    {
        if (AddIn.Stopping) return;
        int id = Interlocked.Increment(ref _postId);
        long queued = Stopwatch.GetTimestamp();
        DiagnosticLog.Trace($"Ribbon.QueueAsMacro.requested; post={id}; {context}");
        ExcelAsyncUtil.QueueAsMacro(() =>
        {
            DiagnosticLog.Trace($"Ribbon.QueueAsMacro.executing; post={id}; queue-ms={DiagnosticLog.MillisecondsSince(queued)}; {context}");
            if (!AddIn.Stopping) action();
        });
    }
    private void PostFailure(int request, Exception ex)
    {
        DiagnosticLog.Failure("Ribbon: opening Avalonia window", ex);
        Post(() =>
        {
            if (request != _request) return;
            _busy = false;
            Transition(State.Ready, "startup failed");
            System.Windows.Forms.MessageBox.Show($"Testfönstret kunde inte öppnas.\n{ex.GetBaseException().Message}\n\nFullständig diagnostik:\n{DiagnosticLog.LogPath}\n\nStarta om Excel efter ett initieringsfel.", "Avalonia Test");
        });
    }
    internal void Stop() { ++_request; _busy = false; _ribbon = null; }
}
