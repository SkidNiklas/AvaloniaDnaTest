# AvaloniaDnaTest

**Senaste tillägg: NativeWebView och HTML-förhandsgranskning (2026-11-10).** Gren `feature/native-webview`. Avalonia.Controls.WebView **12.1.1**, oförändrad Avalonia **12.1.5**, fem byte-identiska inbäddade HTML-mallar, sidomeny och utskrift via WebView2. Alla fem mallar passerar automatiska tester i riktig WebView2; packad XLL har laddats i Excel och inventeringsmallen samt utskriftsdialogen har verifierats visuellt. Egen direkt PDF-export ingår inte: API-provet gav Letter trots A4-CSS. [Integration, resultat och klientprotokoll](docs/native-webview.md). Äldre avsnitt nedan beskriver tidigare testomgångar.

Öppna `dist/AvaloniaDnaTest-x64.xll`, välj **Avalonia Test → Öppna testfönster → HTML-förhandsgranskning**. Välj mall, **Uppdatera** eller **Skriv ut**. Utskrift startar bara efter ett aktivt val. Klienten behöver Excel x64, .NET 8 Desktop Runtime x64 och Microsoft Edge WebView2 Runtime; distribuera enbart XLL-filen. Den aktuella XLL-filen är **9 430 528 byte (8,99 MiB)**.

Utvecklartest: `./scripts/Build.ps1 -WebSmoke` kör även HTML-testerna (behöver installerad WebView2). `python scripts/verify-webview.py` verifierar mallar, WebView-assembly, Ribbon-ikoner och native-payloads i den färdiga XLL-filen. HTML-testernas DOM-resultat sparas under `artifacts/webview/`. Fysisk A4-utskrift, full visuell regression och en ren jobbdator återstår enligt protokollet.

**Aktuell uppföljning:** den fördröjda Open-uppdateringen är undersökt och aktiveringsordningen förbättrad. Grön Open har verifierats visuellt medan Avalonia har fokus. Se [rapporten](artifacts/ribbon-thread-investigation.md) och sista README-avsnittet för aktuella resultat och begränsningar.

**Senaste version: dynamisk Ribbon-feedback.** Ready/Loading/Open, inbäddade ikoner och uppstartsmätning är implementerade. Release-bygge och paketering har 0 fel/varningar. Se avsnittet **Dynamisk Ribbon-feedback – 2026-11-10** sist i dokumentet för aktuella testresultat, begränsningar och klientprotokoll; äldre testresultat nedan är historik.

Tekniskt proof-of-concept för **Avalonia 12.1.5 + Excel-DNA 1.9.0 + .NET 8**, Windows x64. Projektet är fristående och ligger helt i `F:\AvaloniaDnaTest`.

**Resultat 2026-11-10, Skia-felsökning:** ny packad XLL byggd med noll varningar. Felet reproducerades isolerat och avhjälptes med en assemblyspecifik native-resolver. Den nya XLL-filen laddades i Excel x64 och Ribbon-callbackens fönsteröppning slutfördes utan undantag. Alla tre native-modulernas verkliga temporära sökvägar verifierades. Visuell kontroll, upprepade fönstercykler i Excel och ren klient återstår; användaren stoppade datorstyrningen med Escape.

## Innehåll

## Uppföljning av SkiaSharp-felet

Fullständig grundorsaksanalys, ändrad kod, native-imports, tester, källor och återstående klienttest finns i [felsökningsrapporten](artifacts/skia-investigation.md). Diagnostikloggen ligger nu i `%TEMP%\AvaloniaDnaTest\diagnostics.log` och fungerar även före Avalonia-initiering. Hämta hela filen efter testet:

```powershell
Get-Content "$env:TEMP\AvaloniaDnaTest\diagnostics.log" -Raw
Copy-Item "$env:TEMP\AvaloniaDnaTest\diagnostics.log" .\artifacts\client-diagnostics.log
```

Grundorsaken är en exakt namnmatchning i Excel-DNA 1.9: begärt `libSkiaSharp` matchar inte inbäddat `LIBSKIASHARP.DLL`. En assemblyspecifik `SetDllImportResolver` kompletterar namnet med `.dll` innan Excel-DNA:s ALC gör synkron temporär extraktion och laddning. Samma skydd används för HarfBuzz. Registreringen är låst och görs en gång per assembly före UI-start. Inga globala DLL-sökvägar ändras och versionerna är oförändrade.

Kör `.\scripts\Test-Native.ps1` för det isolerade före/efter-testet. `python scripts/verify-native.py` verifierar de verkliga dekomprimerade native-resurserna mot x64-paketen och listar direkta samt fördröjda imports. Loggar finns i `artifacts/native-before.log`, `native-after.log`, `harfbuzz-after.log`, `native-payloads.txt`, `skia-build.log`, `skia-smoke.log` och `excel-runtime-diagnostics.log`.

### Projektinnehåll

| Projekt / katalog | Ansvar |
|---|---|
| `AvaloniaDnaTest.sln` | Visual Studio-solution med tre projekt |
| `src/AvaloniaDnaTest.UI` | XAML, Fluent Theme, enkel MVVM, egen Avalonia-tråd och dispatcher |
| `src/AvaloniaDnaTest.AddIn` | Excel-DNA, Ribbon, COM-läsning på Excel-tråden |
| `src/AvaloniaDnaTest.Host` | Separat utvecklingsvärd och automatiserad runtimekontroll; distribueras inte till klienten |
| `scripts/Build.ps1` | Bygger och kontrollerar de tre stegen |
| `scripts/Inspect-Xll.ps1` | Inspekterar den färdiga PE-filens faktiska resurser utan att exekvera XLL-kod |
| `scripts/Test-Excel.ps1` | Frivillig manuell testhjälp som skapar en separat Excel-instans med testdata |
| `dist/AvaloniaDnaTest-x64.xll` | Packad x64-testkandidat; inga DLL:er eller EXE:er i dist |
| `artifacts` | Lokala byggloggar, testlogg, renderad bild, resurslista och SHA256 |

Gemensam programlogik är tillräckligt liten för att ligga i UI-projektet. Excel-beroendet är ett gränssnitt, `IExcelInformationService`; inga COM-referenser eller Excel-typer förs över till UI-projektet.

## Bygg

Förutsättningar: Windows x64, .NET SDK med stöd för `net8.0-windows`, internetåtkomst till NuGet. Visual Studio med motsvarande .NET-utvecklingsstöd kan öppna solutionen. Denna miljö hade SDK **10.0.403** och .NET 8 Desktop Runtime **8.0.33**; en separat .NET 8 SDK var inte installerad.

Kör i PowerShell från projektroten:

```powershell
.\scripts\Build.ps1
```

Scriptet återställer låsta paketversioner, bygger och kör testvärdens smoke-test, kompilerar Excel-integrationen, packar med Excel-DNA, inspekterar XLL-resurserna och kopierar resultatet till `dist`. Det avbryter vid fel. `-SkipSmoke` kan användas i en byggmiljö utan fungerande Windows-grafik, men innebär att runtime-testet inte körs.

```powershell
# Interaktiv utvecklingsvärd. Stäng fönstret för att avsluta processen.
dotnet run --project src/AvaloniaDnaTest.Host -c Release

# Samma Avalonia-bootstrap som tillägget, tre öppna/stäng-cykler och bindningskontroller.
dotnet src/AvaloniaDnaTest.Host/bin/Release/net8.0-windows/AvaloniaDnaTest.Host.dll --smoke artifacts/test-window.png

# Oberoende kontroll av den färdiga XLL-filen.
.\scripts\Inspect-Xll.ps1
```

Scriptet lägger CLI- och NuGet-cache i arbetsmappen för att också fungera i miljöer med begränsade skrivrättigheter. `packages.lock.json` i varje projekt låser direkta och transitiva versioner och innehållshashar. Ingen preview-version används.

Den här körmiljön blockerade terminalens NuGet-nätverk och skrivning till användarens vanliga cache. Under verifieringen användes därför en tillfällig localhost-reläkälla som hämtade oförändrade paket från NuGet genom det tillgängliga nätverksverktyget, samt offline certifikatrevokeringskontroll. Den lokala, git-ignorerade `NuGet.Local.Config` hör bara till denna session och behövs **inte** för vanliga byggen. `Build.ps1 -NuGetConfig <fil>` kan ange en egen NuGet-konfiguration. Ingen fullständig sårbarhetskontroll av paketen har verifierats genom reläkällan.

## Versioner och API-kompatibilitet

| Paket | Stabil version |
|---|---|
| Avalonia.Win32, Avalonia.Skia, Avalonia.HarfBuzz, Avalonia.Themes.Fluent | 12.1.5 |
| Avalonia.Controls.DataGrid | 12.1.2 |
| ExcelDna.AddIn / ExcelDna.Integration | 1.9.0 |
| SkiaSharp, transitivt | 3.119.4 |
| HarfBuzzSharp, transitivt | 8.3.1.3 |

DataGrid publiceras separat; 12.1.5 fanns inte på NuGet vid kontrollen. Senaste stabila DataGrid var 12.1.2 med beroendet Avalonia >= 12.1.0, vilket tillåter kärnversion 12.1.5. Kombinationen har både kompilerats och körts i testvärden. Excel-DNA 1.10-paketen var preview och används inte.

Avalonia 12 kräver en textformare även vid denna explicita Windows-konfiguration: `.UseWin32().UseSkia().UseHarfBuzz()`. Första runtimeförsöket utan HarfBuzz gav `No text shaping system configured`; det åtgärdades och omtestades. `TextBox.PlaceholderText` och den nya bitmap-encoderöverlagringen används för att undvika föråldrade 12.x-API:er.

## Initiering inuti Excel

`StartWithClassicDesktopLifetime` används inte. Excel äger sin egen huvudtråd, meddelandeloop och processlivstid.

1. `AutoOpen` skapar Excel-tjänsten på Excel-tråden. Avalonia startas först när en Ribbon-knapp används.
2. Ett enda `EmbeddedUi` skapar en dedikerad **STA-tråd**. På den tråden körs `App.Builder().SetupWithoutStarting()` och `Dispatcher.UIThread.MainLoop(...)`.
3. En readiness-signal skickas inifrån dispatchern när dess loop behandlar arbete. Fönster skapas därefter via `Dispatcher.UIThread.InvokeAsync`.
4. Fönstret är modeless. Ribbon-callbacken väntar asynkront, så Excel-tråden fortsätter pumpa meddelanden. Ingen synkron väntan på Excel-information görs från Avalonia.
5. Excels HWND hämtas på Excel-tråden. Efter att Avalonia skapat sitt HWND sätts native top-level-fönsterägarskap med `SetWindowLongPtr(GWLP_HWNDPARENT, excelHwnd)`. Det är ett ägt toppnivåfönster, inte ett child-fönster i kalkylbladet. `Activate()` begär fokus och fönstret visas inte separat i aktivitetsfältet när Excel är värd. Ägarskap och faktisk fokus-/tangentbordsfunktion återstår att verifiera i Excel.
6. Stängning nollställer fönsterreferensen. Nästa Ribbon-klick skapar ett nytt fönster på **samma** Avalonia-runtime och tråd. Vanlig fönsterstängning stoppar inte dispatchern.
7. `AutoClose` stoppar Excel-tjänsten, avbryter väntande informationsförfrågningar, stänger fönstret och begär dispatcher-shutdown. UI-tråden har en begränsad join på tre sekunder. Inga anrop görs till `Environment.Exit`, `Application.Exit` eller någon metod som ska avsluta Excel.

Avalonia har processglobala tjänster och native grafikresurser. Excel-DNA konfigureras med `ExcelAddInDisableAssemblyContextUnload=true` för att inte försöka avlasta laddkontexten medan sådan kod kan finnas kvar. Det innebär medvetet att assemblies och vissa plattformsresurser kan ligga kvar tills Excel-processen avslutas. **Avlasta och återladda själva tillägget kräver en omstart av Excel i denna PoC.** Det är en annan operation än att stänga och återöppna testfönstret, vilket testvärden klarar. Samtidig användning av flera Avalonia-baserade tillägg i samma process har inte testats.

## Excel-kommunikation

Avalonia anropar bara `IExcelInformationService.ReadAsync()`. Implementationen lägger ett arbete i `ExcelAsyncUtil.QueueAsMacro`; Excel-DNA kör arbetet på Excels huvudtråd när Excel är redo för makron och COM. En tråd-ID-kontroll finns i callbacken. Där läses version, aktiv arbetsbok, aktivt blad och aktiv cell med `Value2`. Resultatet är en vanlig sträng. `TaskCompletionSource` med `RunContinuationsAsynchronously` återför resultatet till Avalonia-kontexten.

Ingen arbetsbok ger ett särskilt meddelande. Aktivt diagramblad/cell som inte går att läsa hanteras separat. COM-fel rapporteras i statusfältet. Efter 15 sekunder får användaren ett fel i stället för en obegränsad väntan; ett sent köat arbete ignoreras. Vid stängning av tillägget avbryts väntande anrop. Lokalt förvärvade COM-referenser släpps; `FinalReleaseComObject` används inte.

## En enda XLL och native beroenden

Projektet aktiverar Excel-DNA:s `ExcelDnaPackManagedDependencies` och `ExcelDnaPackNativeLibraryDependencies`, samt `EnableDynamicLoading` så att beroendena finns i byggutmatningen och `.deps.json`. För körning kompletterar nu `PackedNativeLibraries` SkiaSharps och HarfBuzzSharps native-namn med `.dll`. Excel-DNA sköter fortfarande extraktionen. Ingen separat klient-EXE eller egen permanent native-installation behövs.

Den faktiska XLL-filen har inspekterats med `LoadLibraryEx(..., LOAD_LIBRARY_AS_DATAFILE)` och resursenumerering. Där finns bland annat:

- UI- och AddIn-assemblies, Avalonia.Base/Controls/Markup.Xaml/Fluent/DataGrid/HarfBuzz/Skia/Win32, SkiaSharp och HarfBuzzSharp som `ASSEMBLY_LZMA`.
- `LIBSKIASHARP.DLL`, `LIBHARFBUZZSHARP.DLL` och `AV_LIBGLESV2.DLL` som `NATIVE_LIBRARY_LZMA`.
- Den packade DNA-konfigurationen med runtime v8.0 och x64-laddaren.

XAML kompileras av Avalonia-byggsteget och dess resurser ingår i UI-assemblyn. Fluent och DataGrid-resurserna ingår i respektive packad assembly. Detta fungerar i den vanliga testvärden. Att `avares://` och assemblyupplösningen också fungerar när Excel-DNA laddar dem från den packade XLL:en behöver fortfarande verifieras i Excel.

**Temporär extrahering behövs.** Excel-DNA 1.9.0:s `AssemblyManager.NativeLibraryResolve` läser native resursbytes, skapar en temporär katalog och skriver DLL-filen dit. `ExcelDnaAssemblyLoadContext.LoadUnmanagedDll` använder sedan `LoadUnmanagedDllFromPath`. Det är alltså en enda distributionsfil, men inte en lösning utan filer på disk under körning. Skrivbar temporär katalog och möjlighet för Windows att ladda DLL:er därifrån behövs. Inget eget löfte ges om omedelbar borttagning av laddade native DLL:er; laddkontexten behålls här till processens slut. Extraktionen har nu verifierats i Excel via de laddade modulernas absoluta sökvägar i artifacts/excel-runtime-diagnostics.log.

Klienten ska ha Windows x64, **Excel x64** och befintlig **.NET 8 Desktop Runtime x64**. Testvärden och SDK:t behövs bara under utvecklingen. Lägg enbart `dist/AvaloniaDnaTest-x64.xll` i en tom testkatalog på klienten för acceptanstest; inga filer från `bin` ska följa med. Arbetsmappens NuGet-cache behövs inte på klienten. Gällande Excel-/organisationspolicy för tillägg gäller som vanligt.

## Vad har testats?

| Kontroll | Resultat |
|---|---|
| Paketversioner och framework-beroenden | Kontrollerade mot NuGet; inga prerelease-versioner i låsfilerna |
| Steg 1: XAML, Fluent, DataGrid och native rendering i separat värd | Godkänt |
| Samma bootstrap: initiera en gång, öppna/stäng/öppna tre gånger | Godkänt i separat värd |
| TextBox tvåvägsbindning, Slider → ProgressBar, tio DataGrid-rader | Godkänt via riktiga kontroller på Avalonia-tråden |
| Ljust/mörkt tema och asynkront tjänsteresultat till vyn | Godkänt i separat värd med simulerad Excel-tjänst |
| Renderad gränssnittsbild | Skapad i `artifacts/test-window.png` och visuellt granskad |
| Steg 2: AddIn, Ribbon och COM-service kompilerar | Godkänt |
| Steg 3: skapa en enda packad x64-XLL | Godkänt; slutfil i `dist` |
| Inbäddade managed/native PE-resurser | Godkänt via oberoende inspektion av slutfilen |
| Packad XLL laddad i Excel och Ribbon synlig | Godkänt vid Skia-felsökningen |
| Riktiga COM-värden, ingen arbetsbok, cellfel och Excel upptaget | **Ej verifierat** |
| Fönsterägarskap, tangentbord, popupkontroller och Excel-responsivitet | **Ej verifierat i Excel** |
| Native extrahering/laddning i Excel | Godkänt för alla tre bibliotek; AutoClose ej verifierat |
| Ren klient med endast XLL och .NET 8 Desktop Runtime | **Ej verifierat** |

Tidigare COM-försök gav inget verifierbart resultat. Vid Skia-felsökningen kunde Excel startas via computer-use och den nya XLL-filen öppnas från `dist`, som endast innehåller XLL-filen. Ribbon-knappen kördes och loggade `Snapshot: Ribbon window opened`. Alla tre native-moduler laddades från Excel-DNA:s temporära katalog. UI-verktyget kunde inte fånga det separat ägda Avalonia-fönstret som eget målfönster. Användaren stoppade sedan datorstyrningen med Escape; fullständig visuell Excel-acceptans återstår.

## Återstående Excel-acceptanstest

1. Starta Excel x64 i en separat testprocess. Öppna en ny arbetsbok, ange ett känt värde i en cell och markera den. Öppna den packade XLL:en från en katalog med bara XLL-filen.
2. Kontrollera fliken **Avalonia Test** och de tre knapparna **Öppna testfönster**, **Excel-information**, **Om**.
3. Öppna fönstret från Ribbon, skriv svensk text, tabba mellan kontroller och prova ComboBox, båda radioknapparna, CheckBox, DatePicker, Slider och åtgärdsknappar. Växla tema och kontrollera statusfältet.
4. Läs Excel-information, ändra aktiv cell/blad/arbetsbok och uppdatera igen. Prova tom cell, Excel-felvärde, diagramblad och ingen öppen arbetsbok. Prova när Excel är i redigeringsläge eller upptaget.
5. Stäng och återöppna fönstret minst tio gånger via Ribbon. Kontrollera att ett andra klick på Öppna aktiverar det befintliga fönstret. Flytta/minimera Excel, växla fokus och fortsätt redigera kalkylbladet medan Avalonia är öppet.
6. Stäng Excel medan fönstret och en informationsförfrågan är öppna. Kontrollera att processen avslutas och att loggen inte visar fel. Logg: `%TEMP%\AvaloniaDnaTest\diagnostics.log`.
7. Upprepa på en ren klient med bara .NET 8 Desktop Runtime och XLL-filen. Kontrollera native DLL-laddning och temporära filer, exempelvis med Process Monitor. Notera vilka native moduler som faktiskt laddas; alla tre behöver inte användas av varje grafikbackend.

Frivillig hjälp för att skapa testinstansen:

```powershell
.\scripts\Test-Excel.ps1
# I ett annat PowerShell-fönster, efter att testet är klart:
New-Item -ItemType File artifacts/stop-excel-test
```

Hjälpen skapar en egen Excel-instans, en osparad testbok med `AvaloniaPoC!B3 = Hej från Excel` och försöker registrera enbart filen i `dist`. Stoppsignalen stänger testboken utan att spara och avslutar den testinstansen. Den ska bara användas när ingen gammal stoppsignal ligger kvar. Hjälpen kunde inte verifieras till slutförd Excel-laddning i denna miljö.

## Primärkällor

- [Avalonia 12.1.5 på NuGet](https://www.nuget.org/packages/Avalonia/12.1.5)
- [Avalonia DataGrid 12.1.2 och beroenden](https://www.nuget.org/packages/Avalonia.Controls.DataGrid/12.1.2)
- [ExcelDna.AddIn 1.9.0 och versionslista](https://www.nuget.org/packages/ExcelDna.AddIn/1.9.0)
- [Excel-DNA: SDK-projektegenskaper](https://excel-dna.net/docs/guides-basic/sdk-style-project-properties/)
- [Excel-DNA: .NET-runtime](https://excel-dna.net/docs/guides-basic/dotnet-runtime-support/)
- [Avalonia 12.1.5: dispatcher och MainLoop](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Avalonia.Base/Threading/Dispatcher.MainLoop.cs)
- [Excel-DNA 1.9.0: native extrahering](https://github.com/Excel-DNA/ExcelDna/blob/v1.9.0-Release/Source/ExcelDna.ManagedHost/AssemblyManager.cs)
- [Excel-DNA 1.9.0: AssemblyLoadContext](https://github.com/Excel-DNA/ExcelDna/blob/v1.9.0-Release/Source/ExcelDna.ManagedHost/ExcelDnaAssemblyLoadContext.cs)

## Dynamisk Ribbon-feedback – 2026-11-10

Knappen använder `getLabel`, `getImage` och `getEnabled` samt `onLoad` för att spara `IRibbonUI`. Tillstånden är:

| Tillstånd | Text | Ikon | Aktiv |
|---|---|---|---|
| Ready | Öppna testfönster | Blått fönster | Ja |
| Loading | Startar... | Orange laddningspil | Nej |
| Open | Visa testfönster | Grönt fönster | Ja |

Tre PNG-resurser med 32 × 32 pixlar och transparent RGBA-bakgrund är inbäddade i AddIn-assemblyn, som packas i XLL-filen. `scripts/New-RibbonIcons.ps1` återskapar dem. Inga ikonfiler behövs på klienten. `python scripts/verify-ribbon.py` dekomprimerar slutfilens verkliga AddIn-resurs och verifierar assemblybytes, resursnamn, PNG-bytes, storlek och alfakanal samt befintliga native-payloads.

### Schemaläggning och omritning

Klicket sätter Loading och anropar `InvalidateControl("OpenTest")` på Excel-tråden. Callbacken returnerar vid en asynkron `Task.Delay(120)`. Efter denna icke blockerande tidsfrist köas starten med `ExcelAsyncUtil.QueueAsMacro`. Excels HWND hämtas där, den oförändrade native-resolvern konfigureras och den befintliga dedikerade STA-tråden startar Avalonia. Ingen `DoEvents`, `Thread.Sleep` eller `Task.Run` används. Även Excel-information och Om går genom samma spärr mot parallella starter.

120 ms är en möjlighet för Excels meddelandeloop att behandla Ribbon-callbacks och målning, **ingen garanti för avslutad omritning**. Varken en timer eller `QueueAsMacro` är en paint-barriär. Om Excel är upptaget kan callback/målning och makrokön fördröjas; mycket snabba starter kan ge kort synlig Loading. Ingen synkron väntan på målning införs. `InvalidateControl` ogiltigförklarar cachade värden enligt [Microsofts API-dokumentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.office.core.iribbonui.invalidatecontrol); [Excel-DNA beskriver QueueAsMacro](https://excel-dna.net/docs/guides-advanced/performing-asynchronous-work/) för övergång till Excels huvudtråd i makrokontext.

Efter `ShowAsync` köas Open tillbaka till Excel-tråden om fönstret fortfarande är synligt. Om det redan har stängts väljs Ready. `WindowClosed` köar också Ready och kontrollerar aktuell synlighet så att en gammal stängningsnotis inte skriver över en senare öppning. Befintligt fönster återanvänds och aktiveras; minimerat fönster återställs. En separat busy-spärr skyddar även innan Excel hunnit läsa `getEnabled`. Fel loggas genom befintlig `DiagnosticLog.Failure` med hela undantagskedjan, återställer Ready och visar felmeddelande med loggsökväg. En misslyckad global Avalonia-initiering kräver fortfarande omstart av Excel; knappen är åter aktiverad men det utlovas ingen säker ominitiering av den processglobala runtime-miljön. Vid AutoClose ignoreras sena köade UI-uppdateringar.

### Mätning och verifierat resultat

`Stopwatch` mäter klick → Loading, Avalonia setup → dispatcher redo (en gång per runtime) och klick → visat/aktiverat fönster. Loggen anger `cold`, `reopen` eller `activate`, och differensen `cold-minus-current`. Klicktiden inkluderar 120 ms schemaläggning och eventuell väntan på Excel. Synligt betyder att `Show()`/aktivering slutförts och `IsVisible` är satt; första färdigrenderade bildrutan mäts inte. Logg: `%TEMP%\AvaloniaDnaTest\diagnostics.log`.

Verifierat för denna version:

- Hela solutionen byggd i Release och XLL packad: **0 fel, 0 varningar** (`artifacts/ribbon-build.log`).
- Testvärd: tre öppna/stäng-cykler, samma fönster vid aktivering, stängningshändelser/synlighet samt befintliga TextBox-, Slider/ProgressBar-, DataGrid-, tema- och asynkrona servicetester: **PASS** (`artifacts/ribbon-smoke.log`).
- Alla tre ikoner och native-payloads verifierade i färdig XLL; isolerade Skia/HarfBuzz-resolvertester: **PASS** (`artifacts/ribbon-payloads.txt`, `ribbon-native-payloads.txt`). `PackedNativeLibraries.cs` är oförändrad.
- Ny XLL laddad från `dist` i Excel x64. Ready och inaktiverad Loading observerade i Ribbon. Open-texten observerades efter att Excel åter fått fokus. Fönstrets visning och native-laddning loggades utan startfel.
- Excel kallstart: klick → Loading **0,91 ms**, Avalonia-initiering **688,17 ms**, klick → synligt **1 616,80 ms**. Ett klick med redan öppet fönster: **146,41 ms**, skillnad **1 470,39 ms** mot kallstart. Detta är enstaka observationer, inte ett prestandalöfte. Återöppning efter stängning mättes inte i Excel.
- Excel stängdes via sin vanliga stängningsknapp med Avalonia öppet; Excel-processen försvann. Någon separat `AutoClose complete`-rad kunde inte bekräftas i denna körning.

Datorstyrningen kunde inte välja det ägda Avalonia-fönstret som separat målfönster. Faktisk fokusöverföring, fönsterstängning → Ready och återöppning i Excel, felvägen i Excel samt full regression av de tidigare åtta manuella funktionstesterna är därför **inte verifierade för denna version**. Loggkopian finns i `artifacts/ribbon-excel-diagnostics.log`. Inga avsiktliga fel injicerades i den distribuerade XLL-filen.

### Kort testprotokoll på jobbdatorn

Kopiera endast `F:\AvaloniaDnaTest\dist\AvaloniaDnaTest-x64.xll` till en tom användarkatalog. Ingen installation, administratörsrättighet eller medföljande extern DLL behövs för själva tillägget. Befintlig Excel x64 och .NET 8 Desktop Runtime x64 krävs; om runtime saknas är detta klientkrav ännu inte uppfyllt. Excel-DNA extraherar automatiskt inbäddade native-DLL:er till skrivbar temporär katalog under körning. Testa med organisationens ordinarie tilläggspolicy.

| Test | Förväntat | Resultat på klienten |
|---|---|---|
| Ny Excel-process, öppna XLL, första klicket | Blå Ready → orange inaktiverad Startar... → grön Visa testfönster; Excel reagerar under start | Ej kört |
| Snabba dubbelklick och klick på Om/Excel-information under start | En initiering och ett fönster, inga parallella starter | Ej kört |
| Klick med öppet/minimerat fönster | Samma fönster aktiveras/återställs | Ej kört |
| Stäng fönstret och upprepa minst tio gånger | Ready återställs; återöppning använder samma runtime, `reopen` loggas | Ej kört |
| TextBox med svenska tecken, ComboBox, radio, CheckBox, DatePicker, Slider, DataGrid, knappar | Alla befintliga kontroller fungerar; Slider följs av ProgressBar | Ej kört |
| Ljust/mörkt tema och Excel-information/Om | Tema växlar; riktig aktiv cell/bok/blad läses; befintliga felmeddelanden fungerar | Ej kört |
| Startfel i separat testmiljö där fel kan reproduceras | Ready återställs, begriplig dialog och full undantagskedja i loggen; starta om Excel efter globalt initieringsfel | Ej kört |
| Avsluta Excel med fönster och informationsanrop öppna | Normal processavslutning utan kvarvarande Excel-process | Ej kört |

Spara hela diagnostikloggen efter test och jämför `cold` med `reopen`. Notera om Loading faktiskt hann synas och om ikonerna har rätt färg/transparens. Den färdiga distributionskatalogen innehåller enbart XLL-filen.

## Uppföljning: fördröjd Ribbon-uppdatering

Aktuell [felsökningsrapport med trådkarta, före/efter-mätningar, visuell verifiering och begränsningar](artifacts/ribbon-thread-investigation.md). Ingen befintlig COM-trådbrist hittades: InvalidateControl kördes redan på Excels huvudtråd. Excel sköt i den reproducerade körningen upp callbacks när Avalonia redan hade fokus.

Nu visas fönstret först utan automatisk aktivering, Open invalideras via Excel-tråden och efter en asynkron 120 ms-frist begär Avalonia fokus. Väntan på makrot begränsas till 500 ms. Grön ikon och **Visa testfönster** har verifierats visuellt medan Avalonia fortfarande hade fokus. Enabled-uttrycket och native-resolvern är oförändrade. Excel äger fortfarande faktisk callback-/målningstid; ingen paint-garanti ges vid upptaget eller redan inaktivt Excel.

Nya loggrader innehåller apartment, tråd-ID, foreground-HWND, tillstånds-/invaliderings-/callback-tider och makrokötid. Trace-loggningen buffrar disk-I/O; metadata fångas på händelsetråden. Se rapporten för tolkning och loggbegränsningar. Release-bygge: 0 fel/varningar. Livscykel, återanvändning, stängning under aktiveringsfasen, kontroll-/temasmoke och packade resurser passerar. Excel-testet verifierade även återöppning, Ready och normal processavslutning.

Kort klienttest: starta ny Excel-process, öppna endast XLL-filen, klicka och observera Loading → grön Open utan att återaktivera Excel. Stäng och öppna tio gånger; prova befintligt/minimerat fönster, upptaget Excel, startfel och normal avstängning med väntande informationsanrop. Kontrollera `queue-ms`, `since-invalidate-ms`, `since-window-shown-ms` och eventuella `beforeActivation.timeout` i loggen. Full manuell regression och busy-/feltester på jobbdatorn återstår.
