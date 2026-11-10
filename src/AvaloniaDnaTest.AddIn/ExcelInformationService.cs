using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AvaloniaDnaTest.UI;
using ExcelDna.Integration;

namespace AvaloniaDnaTest.AddIn;

public sealed class ExcelInformationService : IExcelInformationService
{
    private readonly int _excelThread = Environment.CurrentManagedThreadId;
    private readonly ConcurrentDictionary<TaskCompletionSource<string>, byte> _pending = new();
    private volatile bool _stopped;
    public async Task<string> ReadAsync()
    {
        ObjectDisposedException.ThrowIf(_stopped, this);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.TryAdd(completion, 0);
        try
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                if (_stopped || completion.Task.IsCompleted) return;
                try
                {
                    if (Environment.CurrentManagedThreadId != _excelThread) throw new InvalidOperationException("Wrong Excel thread.");
                    completion.TrySetResult(ReadOnExcelThread());
                }
                catch (Exception ex) { completion.TrySetException(ex); }
            });
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally { completion.TrySetCanceled(); _pending.TryRemove(completion, out _); }
    }
    public void Stop()
    {
        _stopped = true;
        foreach (var task in _pending.Keys) task.TrySetCanceled();
    }
    private static string ReadOnExcelThread()
    {
        object? application = null, workbook = null, sheet = null, cell = null;
        try
        {
            application = ExcelDnaUtil.Application;
            dynamic app = application;
            string version = Convert.ToString(app.Version) ?? "Okänd";
            workbook = app.ActiveWorkbook;
            if (workbook is null) return $"Excel-version: {version}\nIngen arbetsbok är öppen.";
            string name = Convert.ToString(((dynamic)workbook).Name) ?? "";
            sheet = app.ActiveSheet;
            string sheetName = sheet is null ? "Inget aktivt blad" : Convert.ToString(((dynamic)sheet).Name) ?? "";
            // A chart sheet has no ActiveCell. Report it without failing the other fields.
            string address = "Ej tillgänglig", value = "Ej tillgängligt";
            try
            {
                cell = app.ActiveCell;
                if (cell is not null)
                {
                    address = Convert.ToString(((dynamic)cell).Address) ?? "";
                    value = Convert.ToString(((dynamic)cell).Value2, System.Globalization.CultureInfo.CurrentCulture) ?? "(tom)";
                }
            }
            catch (COMException) { }
            return $"Excel-version: {version}\nArbetsbok: {name}\nKalkylblad: {sheetName}\nAktiv cell: {address}\nVärde: {value}";
        }
        finally
        {
            // Release only locally acquired references, never FinalRelease shared RCWs.
            foreach (var item in new[] { cell, sheet, workbook, application })
                if (item is not null && Marshal.IsComObject(item)) Marshal.ReleaseComObject(item);
        }
    }
}
