using System.Runtime.InteropServices;
using ExcelDna.Integration;
using ExcelDna.Integration.CustomUI;
using AvaloniaDnaTest.UI;

namespace AvaloniaDnaTest.AddIn;

public sealed class AddIn : IExcelAddIn
{
    internal static EmbeddedUi? Ui;
    internal static ExcelInformationService? Excel;
    public void AutoOpen()
    {
        Excel = new ExcelInformationService();
        DiagnosticLog.XllPath = ExcelDnaUtil.XllPath;
        DiagnosticLog.Snapshot("AutoOpen");
        DiagnosticLog.Write($"AutoOpen: Excel thread {Environment.CurrentManagedThreadId}, x64={Environment.Is64BitProcess}");
    }
    public void AutoClose()
    {
        Excel?.Stop();
        Ui?.Dispose();
        DiagnosticLog.Write("AutoClose complete. Restart Excel before reloading an unloaded add-in.");
    }
    internal static async void Show(bool refresh = false, bool about = false)
    {
        try
        {
            // Ribbon invokes this on Excel's thread. No COM object crosses to Avalonia.
            var hwnd = ExcelDnaUtil.WindowHandle;
            PackedNativeLibraries.Configure();
            Ui ??= new EmbeddedUi(Excel ?? throw new InvalidOperationException("Add-in is not open."));
            await Ui.ShowAsync(hwnd, refresh, about);
            DiagnosticLog.Snapshot("Ribbon window opened");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Failure("Ribbon: opening Avalonia window", ex);
            System.Windows.Forms.MessageBox.Show($"Testfönstret kunde inte öppnas.\n{ex.GetBaseException().Message}\n\nFullständig diagnostik:\n{DiagnosticLog.LogPath}\n\nStarta om Excel innan nästa försök efter ett initieringsfel.", "Avalonia Test");
        }
    }
}

[ComVisible(true)]
public sealed class Ribbon : ExcelRibbon
{
    public override string GetCustomUI(string ribbonId) => """
        <customUI xmlns="http://schemas.microsoft.com/office/2009/07/customui">
          <ribbon><tabs><tab id="AvaloniaTestTab" label="Avalonia Test"><group id="AvaloniaTestGroup" label="Testverktyg">
            <button id="OpenTest" label="Öppna testfönster" size="large" imageMso="WindowNew" onAction="OpenWindow"/>
            <button id="ExcelInfo" label="Excel-information" imageMso="Info" onAction="ExcelInformation"/>
            <button id="AboutTest" label="Om" imageMso="Help" onAction="About"/>
          </group></tab></tabs></ribbon>
        </customUI>
        """;
    public void OpenWindow(IRibbonControl control) => AddIn.Show();
    public void ExcelInformation(IRibbonControl control) => AddIn.Show(refresh: true);
    public void About(IRibbonControl control) => AddIn.Show(about: true);
}
