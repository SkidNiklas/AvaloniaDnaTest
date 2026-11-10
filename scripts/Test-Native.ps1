param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath "$PSScriptRoot/..").Path
$hostOutput = "$root/src/AvaloniaDnaTest.Host/bin/$Configuration/net8.0-windows"
$isolated = "$root/artifacts/native-probe-isolated"
New-Item -ItemType Directory -Force $isolated | Out-Null
# Copy managed output only. No runtimes subdirectory or native files may be present.
Copy-Item "$hostOutput/*.*" $isolated -Force
if (Get-ChildItem $isolated -Recurse -Filter libSkiaSharp.dll) { throw 'Probe directory must not contain native libraries.' }
$probe = "$isolated/AvaloniaDnaTest.Host.dll"
$managed = "$root/.packages/skiasharp/3.119.4/lib/net6.0/SkiaSharp.dll"
$native = "$root/.packages/skiasharp.nativeassets.win32/3.119.4/runtimes/win-x64/native"
& dotnet $probe --native-probe $managed $native *> "$root/artifacts/native-before.log"
if ($LASTEXITCODE -ne 1) { throw 'Expected missing-library failure before resolver registration.' }
& dotnet $probe --native-probe $managed $native --fix *> "$root/artifacts/native-after.log"
if ($LASTEXITCODE) { throw 'Skia resolver regression.' }
& dotnet $probe --native-probe "$root/.packages/harfbuzzsharp/8.3.1.3/lib/net6.0/HarfBuzzSharp.dll" "$root/.packages/harfbuzzsharp.nativeassets.win32/8.3.1.3/runtimes/win-x64/native" --fix *> "$root/artifacts/harfbuzz-after.log"
if ($LASTEXITCODE) { throw 'HarfBuzz resolver regression.' }
Write-Output 'PASS: isolated extensionless lookup fails; normalized Skia/HarfBuzz loads succeed; repeated registration is safe. This models the exact-name Excel-DNA lookup; it is not an Excel acceptance test.'
