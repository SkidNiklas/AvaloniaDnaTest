using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;

namespace AvaloniaDnaTest.UI;

public interface IExcelInformationService { Task<string> ReadAsync(); }
public sealed record TestRow(int Id, string Name, int Quantity, decimal Amount);
public sealed class ActionCommand(Action action) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
public sealed class TestViewModel : INotifyPropertyChanged
{
    private readonly IExcelInformationService _excel;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value; PropertyChanged?.Invoke(this, new(name));
        if (name != nameof(Status) && name != nameof(ExcelInformation)) Status = $"{DateTime.Now:HH:mm:ss} · {name} ändrades";
    }
    private string _input = "Hej från Avalonia", _option = "Alternativ A", _status = "Redo", _information = "Tryck på Uppdatera från Excel.";
    private bool _testMode = true, _standard = true, _dark = true;
    private double _progress = 35;
    private DateTimeOffset? _date = DateTimeOffset.Now;
    public string Input { get => _input; set => Set(ref _input, value); }
    public string SelectedOption { get => _option; set => Set(ref _option, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public string ExcelInformation { get => _information; set => Set(ref _information, value); }
    public bool TestMode { get => _testMode; set => Set(ref _testMode, value); }
    public bool StandardMode { get => _standard; set => Set(ref _standard, value); }
    public bool DarkTheme { get => _dark; set { Set(ref _dark, value); Application.Current!.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light; } }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public DateTimeOffset? SelectedDate { get => _date; set => Set(ref _date, value); }
    public string[] Options { get; } = ["Alternativ A", "Alternativ B", "Alternativ C"];
    public TestRow[] Rows { get; } = Enumerable.Range(1, 10).Select(i => new TestRow(i, $"Testprodukt {i:00}", i * 2, i * 129.50m)).ToArray();
    public ICommand ApplyCommand { get; }
    public ICommand AdvanceCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AboutCommand { get; }
    public TestViewModel(IExcelInformationService excel)
    {
        _excel = excel;
        ApplyCommand = new ActionCommand(() => Status = $"{Input} · {SelectedOption} · Test: {TestMode} · {(StandardMode ? "Standard" : "Utökad")} · {SelectedDate:yyyy-MM-dd}");
        AdvanceCommand = new ActionCommand(() => Progress = Math.Min(100, Progress + 10));
        ResetCommand = new ActionCommand(() => { Input = "Hej från Avalonia"; SelectedOption = Options[0]; TestMode = true; StandardMode = true; SelectedDate = DateTimeOffset.Now; Progress = 0; Status = "Återställd"; });
        RefreshCommand = new ActionCommand(async () => await RefreshAsync());
        AboutCommand = new ActionCommand(() => Status = "Tekniskt proof-of-concept · Avalonia 12.1.5 / .NET 8 / Excel-DNA 1.9.0 · x64");
    }
    public async Task RefreshAsync()
    {
        try { ExcelInformation = await _excel.ReadAsync(); Status = "Excel-information uppdaterad"; }
        catch (Exception ex) { Status = $"Kunde inte läsa Excel: {ex.Message}"; }
    }
}
