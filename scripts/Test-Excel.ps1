param([string]$Xll = "$PSScriptRoot/../dist/AvaloniaDnaTest-x64.xll")
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath "$PSScriptRoot/../artifacts/stop-excel-test") { throw 'Remove the old artifacts/stop-excel-test signal before starting a new test.' }
New-Item -ItemType Directory -Force "$PSScriptRoot/../artifacts" | Out-Null
# Dedicated instance: never attach to an existing user's Excel session.
$excel = New-Object -ComObject Excel.Application
$book = $null
$books = $null
$sheets = $null
$sheet = $null
$cell = $null
try {
    $excel.Visible = $true
    $books = $excel.Workbooks
    $book = $books.Add()
    $sheets = $book.Worksheets
    $sheet = $sheets.Item(1)
    $sheet.Name = 'AvaloniaPoC'
    $cell = $sheet.Range('B3')
    $cell.Value2 = 'Hej från Excel'
    $cell.Select()
    $path = (Resolve-Path -LiteralPath $Xll).Path
    if (-not $excel.RegisterXLL($path)) { throw 'RegisterXLL returned false.' }
    Write-Output "REGISTERED: $path; Excel $($excel.Version); HWND $($excel.Hwnd)"
    Write-Output 'Use Ribbon to test. Create artifacts/stop-excel-test to close this test instance without saving.'
    while (-not (Test-Path -LiteralPath "$PSScriptRoot/../artifacts/stop-excel-test")) { Start-Sleep -Seconds 1 }
}
finally {
    try { if ($book) { $book.Close($false) } }
    catch { Write-Output 'Test workbook already disconnected or closed.' }
    try { $excel.Quit() }
    catch { Write-Output 'Excel already disconnected or closed.' }
    foreach ($reference in @($cell, $sheet, $sheets, $book, $books, $excel)) {
        if ($null -ne $reference) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($reference) }
    }
    $cell = $sheet = $sheets = $book = $books = $excel = $null
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
