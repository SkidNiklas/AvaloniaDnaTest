param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$NuGetConfig,
    [switch]$SkipSmoke,
    [switch]$WebSmoke
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath "$PSScriptRoot/..").Path
Push-Location $root
try {
    $env:DOTNET_CLI_HOME = Join-Path $root '.cli'
    $env:NUGET_PACKAGES = Join-Path $root '.packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $root '.nuget-http'
    New-Item -ItemType Directory -Force artifacts,dist | Out-Null
    $restoreArgs = @('restore','AvaloniaDnaTest.sln','--locked-mode','--disable-parallel','-m:1')
    if ($NuGetConfig) { $restoreArgs += @('--configfile', $NuGetConfig) }
    & dotnet @restoreArgs 2>&1 | Tee-Object artifacts/restore.log
    if ($LASTEXITCODE) { throw 'NuGet restore failed.' }
    # Step 1: build and run the same custom dispatcher bootstrap in a separate host.
    & dotnet build src/AvaloniaDnaTest.Host -c $Configuration --no-restore -m:1 -p:UseSharedCompilation=false 2>&1 | Tee-Object artifacts/step1-build.log
    if ($LASTEXITCODE) { throw 'Step 1 build failed.' }
    if (-not $SkipSmoke) {
        & dotnet "src/AvaloniaDnaTest.Host/bin/$Configuration/net8.0-windows/AvaloniaDnaTest.Host.dll" --smoke artifacts/test-window.png 2>&1 | Tee-Object artifacts/step1-smoke.log
        if ($LASTEXITCODE) { throw 'Step 1 runtime smoke failed.' }
    }
    if ($WebSmoke) {
        & dotnet "src/AvaloniaDnaTest.Host/bin/$Configuration/net8.0-windows/AvaloniaDnaTest.Host.dll" --web-smoke 2>&1 | Tee-Object artifacts/webview-smoke.log
        if ($LASTEXITCODE) { throw 'NativeWebView runtime smoke failed.' }
    }
    # Step 2: compile the integration independently of packing.
    & dotnet build src/AvaloniaDnaTest.AddIn -c $Configuration --no-restore -m:1 -p:UseSharedCompilation=false -p:RunExcelDnaPack=false 2>&1 | Tee-Object artifacts/step2-build.log
    if ($LASTEXITCODE) { throw 'Step 2 build failed.' }
    # Step 3: Excel-DNA build task packs dependencies listed in deps.json.
    & dotnet build src/AvaloniaDnaTest.AddIn -c $Configuration --no-restore -m:1 -p:UseSharedCompilation=false -p:RunExcelDnaPack=true 2>&1 | Tee-Object artifacts/step3-pack.log
    if ($LASTEXITCODE) { throw 'Step 3 packing failed.' }
    $packed = "src/AvaloniaDnaTest.AddIn/bin/$Configuration/net8.0-windows/publish/AvaloniaDnaTest.AddIn-AddIn64-packed.xll"
    if (-not (Test-Path -LiteralPath $packed)) { throw 'Packed XLL is missing.' }
    & "$PSScriptRoot/Inspect-Xll.ps1" -Xll $packed | Tee-Object artifacts/packed-resources.txt
    Copy-Item -LiteralPath $packed -Destination dist/AvaloniaDnaTest-x64.xll -Force
    Get-FileHash dist/AvaloniaDnaTest-x64.xll -Algorithm SHA256 | Format-List | Out-File artifacts/xll-sha256.txt
    Write-Output 'Built dist/AvaloniaDnaTest-x64.xll. Excel loading and UI acceptance must be verified separately.'
}
finally { Pop-Location }
