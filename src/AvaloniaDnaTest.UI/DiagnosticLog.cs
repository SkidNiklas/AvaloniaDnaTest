using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Collections.Concurrent;

namespace AvaloniaDnaTest.UI;

// BCL only: usable before App.Builder or any Skia static initializer runs.
public static class DiagnosticLog
{
    private static readonly object Gate = new();
    private static readonly object TraceDrainGate = new();
    private static readonly ConcurrentQueue<string> TraceLines = new();
    private static int _traceScheduled;
    private static nint _excelWindow;
    private static long _windowShown;
    public static nint ExcelWindow { get => Interlocked.CompareExchange(ref _excelWindow, 0, 0); set => Interlocked.Exchange(ref _excelWindow, value); }
    public static long WindowShownTimestamp => Interlocked.Read(ref _windowShown);
    public static void WindowShown() { Interlocked.Exchange(ref _windowShown, Stopwatch.GetTimestamp()); Trace("Avalonia.WindowShown"); }
    public static string MillisecondsSince(long timestamp) => timestamp == 0 ? "n/a" : Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

    // Capture on the calling thread; only disk I/O is moved to a single queued drain.
    // No COM access, UI dispatch, synchronous disk write or focus change in this path.
    public static void Trace(string context)
    {
        try
        {
            var now = DateTimeOffset.Now;
            long tick = Stopwatch.GetTimestamp();
            nint foreground = GetForegroundWindow();
            nint excel = ExcelWindow;
            string focus = excel == 0 ? "unknown" : (foreground == excel).ToString();
            TraceLines.Enqueue($"{now:O} PID={Environment.ProcessId} thread={Environment.CurrentManagedThreadId} apartment={Thread.CurrentThread.GetApartmentState()} tick={tick} foreground=0x{foreground:X} excel=0x{excel:X} excelForeground={focus} {context}{Environment.NewLine}");
            if (Interlocked.Exchange(ref _traceScheduled, 1) == 0)
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    do
                    {
                        FlushTrace();
                        Interlocked.Exchange(ref _traceScheduled, 0);
                    } while (!TraceLines.IsEmpty && Interlocked.Exchange(ref _traceScheduled, 1) == 0);
                });
        }
        catch { /* Diagnostic capture must never break an Office callback. */ }
    }
    public static void FlushTrace()
    {
        try
        {
            lock (TraceDrainGate)
            {
                var batch = new StringBuilder();
                while (TraceLines.TryDequeue(out var line)) batch.Append(line);
                if (batch.Length == 0) return;
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                    File.AppendAllText(LogPath, batch.ToString());
                }
            }
        }
        catch { }
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    public static string LogPath => Path.Combine(Path.GetTempPath(), "AvaloniaDnaTest", "diagnostics.log");
    public static string? XllPath { get; set; }
    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} PID={Environment.ProcessId} thread={Environment.CurrentManagedThreadId} {message}{Environment.NewLine}");
            }
        }
        catch { /* Never propagate a logging failure into Excel. */ }
    }

    public static void Failure(string context, Exception exception)
    {
        try
        {
            var text = new StringBuilder(context).AppendLine().AppendLine(exception.ToString());
            Append(exception, "root");
            Write(text.ToString());
            Snapshot(context);
            void Append(Exception ex, string label)
            {
                text.AppendLine($"{label}: type={ex.GetType().AssemblyQualifiedName}; HRESULT=0x{ex.HResult:X8}")
                    .AppendLine(ex.ToString()).AppendLine(ex.StackTrace);
                if (ex is AggregateException aggregate)
                    for (int i = 0; i < aggregate.InnerExceptions.Count; i++) Append(aggregate.InnerExceptions[i], $"{label}[{i}]");
                else if (ex.InnerException is { } inner) Append(inner, label + ".InnerException");
                if (ex is ReflectionTypeLoadException reflection)
                    foreach (var loader in reflection.LoaderExceptions) if (loader != null) Append(loader, label + ".LoaderException");
            }
        }
        catch { Write(context + ": failed to collect exception details."); }
    }

    public static void Snapshot(string context)
    {
        try
        {
            var text = new StringBuilder($"Snapshot: {context}\n")
                .AppendLine($"Process={RuntimeInformation.ProcessArchitecture}; OS={RuntimeInformation.OSArchitecture}; {RuntimeInformation.OSDescription}")
                .AppendLine($"Runtime={RuntimeInformation.FrameworkDescription}; Environment.Version={Environment.Version}")
                .AppendLine($"BaseDirectory={AppContext.BaseDirectory}\nCurrentDirectory={Environment.CurrentDirectory}\nXLL={XllPath ?? "unavailable"}");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName))
            {
                var name = assembly.GetName().Name ?? "";
                if (name.StartsWith("Avalonia") || name.StartsWith("ExcelDna") || name.Contains("Skia") || name.Contains("HarfBuzz"))
                    text.AppendLine($"Assembly={assembly.FullName}; Location={assembly.Location}; ALC={AssemblyLoadContext.GetLoadContext(assembly)?.Name}; MVID={assembly.ManifestModule.ModuleVersionId}");
            }
            using var process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
                if (module.ModuleName.Contains("skia", StringComparison.OrdinalIgnoreCase) || module.ModuleName.Contains("harfbuzz", StringComparison.OrdinalIgnoreCase) || module.ModuleName.Contains("gles", StringComparison.OrdinalIgnoreCase))
                    text.AppendLine($"Native={module.FileName}; version={module.FileVersionInfo.FileVersion}; base=0x{module.BaseAddress:X}");
            Write(text.ToString());
        }
        catch (Exception ex) { Write("Snapshot incomplete: " + ex); }
    }
}
