# SkiaSharp-felsökning 2026-11-10

## Grundorsak

Excel-DNA 1.9.0:s AssemblyManager.NativeLibraryResolve slår upp begärt namn exakt, konverterat till versaler. SkiaSharp 3.119.4:s faktiska Windows/.NET-P/Invoke begär `libSkiaSharp`, men resursen heter `LIBSKIASHARP.DLL`. Extraktion startar därför inte. Utan lösa native-filer misslyckas Windows normala laddning. Extraktionen är synkron; det finns ingen separat extraktionsaktivitet att invänta.

NativeProbe laddar den verkliga managed-assemblyn från bytes (tom Assembly.Location) i en egen ALC som modellerar Excel-DNA:s exakta namnsökning. Testprogrammet körs från en isolerad katalog utan native assets. Före ändringen uppstår exakt SKImageInfo-felet, med DllNotFoundException och HRESULT 0x8007007E. Efter namnnormalisering lyckas samma statiska initiering. Se native-before.log och native-after.log. Ursprunglig Excel-körning saknar full inner exception; dess historiska fel kan därför inte bevisas från det korta meddelandet ensamt. Den konkreta laddningsdefekten är reproducerad och korrigerad.

## Kod och laddning

PackedNativeLibraries.Configure körs från Ribbon före new EmbeddedUi och före Avalonia/Skia-initiering. NativeLibrary.SetDllImportResolver registreras för SkiaSharp och HarfBuzzSharp en gång per assembly under lås. En befintlig extern resolver skrivs aldrig över; registreringsfel rapporteras. Endast respektive biblioteksnamn hanteras. NativeLibrary.Load med `.dll`, requestingAssembly och flags går till assemblyns ALC utan att återanropa samma resolver. Excel-DNA extraherar då rätt resurs och laddar dess absoluta temporära sökväg. HarfBuzz behandlas likadant.

Inga globala ändringar av PATH, arbetskatalog eller DLL-sökkataloger görs. Native-handtag behålls under processens livstid; befintlig Avalonia-dispatcher återanvänds mellan fönsteröppningar. Omladdning av själva tillägget efter AutoClose kräver fortsatt Excel-omstart enligt befintlig arkitektur.

DiagnosticLog använder bara BCL och fungerar innan Avalonia initieras. Ribbon- och dispatcherfel loggar full Exception.ToString, rekursiva inner exceptions inklusive AggregateException/LoaderExceptions, stackar, typ och HRESULT. Snapshot innehåller process-ID, tråd, OS/processarkitektur, runtime, BaseDirectory, aktuell katalog, XLL-sökväg, assemblies/versioner/MVID/ALC samt native-modulsökvägar. Ett vanligt Windows-meddelande visar loggsökvägen. Loggningsfel får inte kasta vidare in i Excel.

## Versioner och imports

Versionerna ändrades inte. Avalonia.Skia 12.1.5 kräver SkiaSharp >=3.119.4. Låsfilerna väljer 3.119.4 för både SkiaSharp och NativeAssets.Win32. HarfBuzzSharp och NativeAssets.Win32 är båda 8.3.1.3. Inga motstridiga paketversioner påträffades. AssemblyVersion skiljer sig från paketversion: SkiaSharp rapporterar 3.119.0.0 och HarfBuzzSharp 1.0.0.0; det är ingen paketkonflikt.

verify-native.py läser den faktiska XLL-filens PE-resurser, dekomprimerar LZMA, jämför hela DLL-innehållet med NuGet x64-assets och kontrollerar AMD64-maskintyp. Alla tre matchar. libSkiaSharp importerar ole32, FONTSUB, USER32, KERNEL32 samt fördröjda d3d12 och D3DCOMPILER_47. Ingen separat MSVC-runtime-DLL importeras av denna binär. HarfBuzz importerar KERNEL32. ANGLE importerar USER32, KERNEL32, dxgi och Windows API-set för synkronisering. Dessa är Windows-komponenter. GPU/backend och klientpolicy återstår att prova på målklienten. Rapport: native-payloads.txt.

## Verifiering

- Hela solutionen byggd Release med 0 varningar och 0 fel, inklusive packning: skia-build.log.
- I den begränsade byggmiljön användes -p:UsedAvaloniaProducts= för att stänga av byggtelemetri vars logg annars skrevs utanför arbetsmappens skrivrättigheter. Inga runtimeinställningar ändrades.
- Isolerad före/efter-reproduktion, HarfBuzz-test och upprepad resolverregistrering godkända. Kör scripts/Test-Native.ps1 efter bygge; kräver projektets lokala paketcache.
- Testvärdens tre öppna/stäng-cykler, TextBox-tvåvägsbindning, Slider/ProgressBar, tio DataGrid-rader och båda teman godkända: skia-smoke.log.
- Managed/native-resurser verifierade: packed-resources.txt. Kör scripts/Inspect-Xll.ps1 och python scripts/verify-native.py från projektroten.
- Packad XLL laddad från dist i Excel x64/.NET 8.0.33. Ribbon-knappen kördes och ShowAsync slutfördes utan undantag. Loggen bekräftar alla tre native-modulerna under `%TEMP%\ExcelDna.Host\d30a499d-e2c4-48a1-b5e8-9a5037859871\ExcelDna.ManagedHost\`. Inga andra Skia/HarfBuzz-moduler syntes före starten. Se excel-runtime-diagnostics.log.
- Visuell kontroll och upprepad användning i Excel återstår efter att användaren stoppade datorstyrningen med Escape. AutoClose och ren klient har inte verifierats.
- Distribution: endast F:\AvaloniaDnaTest\dist\AvaloniaDnaTest-x64.xll. SHA256: EA1E044A57C9A8A9832A53576174A3C1218C055957AC67BEE540738968CD969E.

## Nästa klienttest

Starta en ny Excel x64-process. Öppna just denna XLL från en katalog utan andra filer. Klicka Öppna testfönster, kontrollera kontroller och tema, stäng/öppna minst tio gånger och prova Excel-information. Kontrollera också Excel-avslut. Efter statiskt initieringsfel krävs Excel-omstart, eftersom .NET behåller misslyckad typinitiering.

Hämta hela `%TEMP%\AvaloniaDnaTest\diagnostics.log` efter testet. Loggen fylls på; PID och tidsstämpel skiljer sessionerna åt.

```powershell
Get-Content "$env:TEMP\AvaloniaDnaTest\diagnostics.log" -Raw
Copy-Item "$env:TEMP\AvaloniaDnaTest\diagnostics.log" .\artifacts\client-diagnostics.log
```

## Primärkällor

- https://github.com/Excel-DNA/ExcelDna/blob/v1.9.0-Release/Source/ExcelDna.ManagedHost/ExcelDnaAssemblyLoadContext.cs
- https://github.com/Excel-DNA/ExcelDna/blob/v1.9.0-Release/Source/ExcelDna.ManagedHost/AssemblyManager.cs
- https://github.com/mono/SkiaSharp/blob/v3.119.4/binding/SkiaSharp/SkiaApi.cs
- Lokala NuGet-nuspec-filer och låsfiler; faktiskt P/Invoke-namn observerat både i isoleringstestet och i Excel-loggen.
