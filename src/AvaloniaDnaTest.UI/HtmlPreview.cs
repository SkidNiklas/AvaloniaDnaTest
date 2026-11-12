using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AvaloniaDnaTest.UI;

public sealed class HtmlPreview : UserControl
{
    private readonly ComboBox _templates = new() { MinWidth = 180 };
    private readonly Button _reload = new() { Content = "Uppdatera" };
    private readonly Button _print = new() { Content = "Skriv ut", IsEnabled = false };
    private readonly TextBlock _status = new() { Text = "Startar WebView2…", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ContentControl _browserHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _adapter = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _initialLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task InitialLoad => _initialLoad.Task;
    private readonly SemaphoreSlim _operations = new(1);
    private NativeWebView? _web;
    private TaskCompletionSource? _navigation;
    private bool _started, _closed, _ready, _settingSelection;
    public string? CurrentTemplate { get; private set; }
    public string Status => _status.Text ?? "";
    public static string UserDataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AvaloniaDnaTest", "WebView2");

    public HtmlPreview()
    {
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        toolbar.Children.Add(_templates); toolbar.Children.Add(_reload); toolbar.Children.Add(_print);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 12 };
        layout.Children.Add(toolbar); Grid.SetRow(_status, 1); layout.Children.Add(_status);
        Grid.SetRow(_browserHost, 2); layout.Children.Add(_browserHost); Content = layout;
        _templates.ItemsSource = HtmlTemplates.Names;
        _templates.SelectedIndex = 0;
        _templates.SelectionChanged += async (_, _) =>
        {
            if (_settingSelection || !_started || _templates.SelectedItem is not string name) return;
            await RunUiAsync(() => LoadTemplateAsync(name));
        };
        _reload.Click += async (_, _) => await RunUiAsync(() => LoadTemplateAsync((string)_templates.SelectedItem!));
        _print.Click += async (_, _) => await RunUiAsync(ShowPrintAsync);
        AttachedToVisualTree += async (_, _) =>
        {
            if (_started) return;
            _started = true;
            await RunUiAsync(async () =>
            {
                try
                {
                    InitializeWebView();
                    await LoadTemplateAsync(HtmlTemplates.Names[0]);
                    _initialLoad.TrySetResult();
                }
                catch (Exception ex)
                {
                    _initialLoad.TrySetException(ex);
                    if (!_adapter.Task.IsCompletedSuccessfully)
                    {
                        _adapter.TrySetException(ex);
                        Dispatcher.UIThread.UnhandledException -= InitializationFailed;
                        _browserHost.Content = null;
                    }
                    throw;
                }
            });
        };
    }

    private void InitializeWebView()
    {
        DiagnosticLog.Write("NativeWebView initialization requested; data=" + UserDataFolder);
        var info = WebViewAdapterInfo.GetAdapterInfo(WebViewAdapterType.WebView2);
        DiagnosticLog.Write("WebView2 runtime availability: " + info);
        if (!info.IsInstalled || !info.IsSupported)
            throw new InvalidOperationException("Microsoft Edge WebView2 Runtime saknas eller stöds inte. Installera WebView2 Evergreen Runtime x64 och starta om Excel. " + info.UnavailableReason);
        Directory.CreateDirectory(UserDataFolder);
        _web = new NativeWebView
        {
            AdapterPreference = new[] { WebViewAdapterType.WebView2 },
            Background = Avalonia.Media.Brushes.White
        };
        _web.EnvironmentRequested += (_, args) =>
        {
            if (args is WindowsWebView2EnvironmentRequestedEventArgs windows)
            {
                windows.UserDataFolder = UserDataFolder;
                windows.ProfileName = "HtmlPreview";
                windows.Language = "sv-SE";
                DiagnosticLog.Write("NativeWebView EnvironmentRequested; application-specific profile configured.");
            }
        };
        _web.AdapterCreated += (_, _) =>
        {
            DiagnosticLog.Write("NativeWebView AdapterCreated: " + _web.AdapterInfo);
            _adapter.TrySetResult();
        };
        _web.AdapterDestroyed += (_, _) =>
        {
            DiagnosticLog.Write("NativeWebView AdapterDestroyed");
            _ready = false; _navigation?.TrySetCanceled(); SetBusy(false);
        };
        _web.NavigationStarted += (_, e) => DiagnosticLog.Write($"HTML navigation start: {CurrentTemplate}; scheme={e.Request?.Scheme}");
        _web.NavigationCompleted += (_, e) =>
        {
            DiagnosticLog.Write($"HTML navigation completed: {CurrentTemplate}; success={e.IsSuccess}; scheme={e.Request?.Scheme}");
            if (e.IsSuccess) _navigation?.TrySetResult();
            else _navigation?.TrySetException(new InvalidOperationException("WebView2 kunde inte läsa HTML-mallen."));
        };
        _web.NewWindowRequested += (_, e) => e.Handled = true;
        _web.WebMessageReceived += (_, e) => DiagnosticLog.Write("HTML JavaScript message: " + e.Body);
        // The package reports asynchronous creation failures through the dispatcher,
        // rather than an initialization-failed event. Handle only WebView stack frames
        // during this control's initialization; never swallow unrelated UI exceptions.
        Dispatcher.UIThread.UnhandledException += InitializationFailed;
        _browserHost.Content = _web;
    }

    private void InitializationFailed(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (_closed || _adapter.Task.IsCompleted || !e.Exception.ToString().Contains("Avalonia.Controls", StringComparison.Ordinal)
            || !e.Exception.ToString().Contains("WebView", StringComparison.Ordinal)) return;
        DiagnosticLog.Failure("NativeWebView asynchronous initialization", e.Exception);
        e.Handled = true;
        _adapter.TrySetException(new InvalidOperationException("WebView2 kunde inte initieras. Se diagnostikloggen.", e.Exception));
    }

    public async Task LoadTemplateAsync(string name)
    {
        await _operations.WaitAsync(_lifetime.Token);
        try
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            SetBusy(true); _ready = false;
            SetStatus("Initierar WebView2…");
            await _adapter.Task.WaitAsync(TimeSpan.FromSeconds(25), _lifetime.Token);
            Dispatcher.UIThread.UnhandledException -= InitializationFailed;
            CurrentTemplate = name;
            _settingSelection = true;
            try { _templates.SelectedItem = name; } finally { _settingSelection = false; }
            SetStatus("Läser " + name + "…");
            _navigation = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _web!.NavigateToString(HtmlTemplates.Preview(name));
            await _navigation.Task.WaitAsync(TimeSpan.FromSeconds(20), _lifetime.Token);
            await WaitForLayoutAsync();
            string errors = await InspectAsync("JSON.stringify(window.__previewErrors)") ?? "null";
            DiagnosticLog.Write($"HTML JavaScript errors {name}: {errors}");
            var errorList = JsonSerializer.Deserialize<string[]>(JsonSerializer.Deserialize<string>(errors) ?? "[]");
            if (errorList is { Length: > 0 }) throw new InvalidOperationException("JavaScript-fel: " + string.Join("; ", errorList));
            _ready = true;
            SetStatus(name + " · Redo · exempeldata från inbäddad mall");
        }
        finally { SetBusy(false); _operations.Release(); }
    }

    private async Task WaitForLayoutAsync()
    {
        for (int i = 0; i < 100; i++)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            if (await InspectAsync("window.__previewReady === true && document.readyState === 'complete'") == "true") return;
            await Task.Delay(100, _lifetime.Token);
        }
        throw new TimeoutException("HTML-layouten blev inte redo för utskrift inom 10 sekunder.");
    }

    public async Task<string?> InspectAsync(string script) => await _web!.InvokeScript(script)
        .WaitAsync(TimeSpan.FromSeconds(10), _lifetime.Token);

    public async Task ShowPrintAsync()
    {
        if (!_ready) throw new InvalidOperationException("Vänta tills HTML-mallen är redo.");
        await _operations.WaitAsync(_lifetime.Token);
        try
        {
            SetBusy(true);
            await WaitForLayoutAsync();
            DiagnosticLog.Write("HTML ShowPrintUI requested: " + CurrentTemplate);
            _web!.ShowPrintUI(); // Native WebView2 API; preview window.print remains suppressed.
            SetStatus("Utskriftsdialog begärd · välj A4, orientering och bakgrundsgrafik i dialogen.");
        }
        finally { SetBusy(false); _operations.Release(); }
    }

    private async Task RunUiAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception ex)
        {
            DiagnosticLog.Failure("HTML preview: " + CurrentTemplate, ex);
            if (!_closed) SetStatus(ex.Message + " Logg: " + DiagnosticLog.LogPath);
        }
    }
    private void SetStatus(string text) { if (!_closed) _status.Text = text; }
    private void SetBusy(bool busy)
    {
        _templates.IsEnabled = _reload.IsEnabled = !busy && !_closed;
        _print.IsEnabled = !busy && _ready && !_closed;
    }
    public void Close()
    {
        if (_closed) return;
        _closed = true; _ready = false;
        _lifetime.Cancel(); _navigation?.TrySetCanceled(); _initialLoad.TrySetCanceled();
        Dispatcher.UIThread.UnhandledException -= InitializationFailed;
        _browserHost.Content = null; // Detaching NativeWebView disposes its native adapter/controller.
        SetBusy(false);
        DiagnosticLog.Write("NativeWebView closed; adapter detached; profile retained.");
    }
}
