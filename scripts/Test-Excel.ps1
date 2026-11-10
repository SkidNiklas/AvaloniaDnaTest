param([string]$Xll = "$PSScriptRoot/../dist/AvaloniaDnaTest-x64.xll")
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath "$PSScriptRoot/../artifacts/stop-excel-test") { throw 'Remove the old artifacts/stop-excel-test signal before starting a new test.' }
New-Item -ItemType Directory -Force "$PSScriptRoot/../artifacts" | Out-Null
# Dedicated instance: never attach to an existing user's Excel session.
$excel = New-Object -ComObject Excel.Application
$book = $null
try {
    $excel.Visible = $true
    $book = $excel.Workbooks.Add()
    $book.Worksheets.Item(1).Name = 'AvaloniaPoC'
    $book.Worksheets.Item(1).Range('B3').Value2 = 'Hej från Excel'
    $book.Worksheets.Item(1).Range('B3').Select()
    $path = (Resolve-Path -LiteralPath $Xll).Path
    if (-not $excel.RegisterXLL($path)) { throw 'RegisterXLL returned false.' }
    Write-Output "REGISTERED: $path; Excel $($excel.Version); HWND $($excel.Hwnd)"
    Write-Output 'Use Ribbon to test. Create artifacts/stop-excel-test to close this test instance without saving.'
    while (-not (Test-Path -LiteralPath "$PSScriptRoot/../artifacts/stop-excel-test")) { Start-Sleep -Seconds 1 }
}
finally {
    if ($book) { $book.Close($false) }
    $excel.Quit()
    if ($book) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($book) }
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
}
