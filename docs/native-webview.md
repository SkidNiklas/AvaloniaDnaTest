# NativeWebView i Excel-DNA – 2026-11-10

## Leverans och versioner

- Gren: `feature/native-webview`.
- `Avalonia.Controls.WebView` **12.1.1**, senaste stabila version vid arbetet. Paketet har `net8.0`-assembly, MIT-licens och lägsta Avalonia-beroende 12.0.0. Projektets låsta beroendegraf använder fortfarande Avalonia **12.1.5** och DataGrid **12.1.2**.
- Testad WebView2 Runtime: **157.0.4341.42**, Windows x64; Excel **16.0**, x64.
- Distribution: endast `dist/AvaloniaDnaTest-x64.xll`, **9 430 016 byte**, cirka **8,99 MiB**.
- SHA256: `314E2DBE46C01940528A9869131DFAFB0965E3134B4BB7E65F4A452026B5D3B1`.

Klienten behöver befintlig Excel x64, .NET 8 Desktop Runtime x64 och Edge WebView2 Runtime. Ingen testvärd, HTML-fil eller separat DLL ska följa med. Organisationens vanliga tilläggspolicy gäller.

## Integration

`MainWindow` har en infällbar `SplitView` med Översikt, UI-kontroller, HTML-förhandsgranskning och Inställningar. HTML-sidan ligger i en `ContentControl`. De tidigare testkontrollerna och deras bindningar finns kvar på kontrollsidan. Temaväxlingen är gemensam. Dokumentytan har vit native-bakgrund även i mörkt app-tema. Fönstret startar centrerat för att undvika delvis osynliga menyer på flera skärmar.

`HtmlPreview` skapar `NativeWebView` först när HTML-sidan väljs. Endast WebView2-adaptern tillåts. `WebViewAdapterInfo.GetAdapterInfo` kontrollerar runtime och loggar version; saknad runtime ger en begriplig svensk status med installationskrav och loggsökväg.

`EnvironmentRequested` anger `%LOCALAPPDATA%\AvaloniaDnaTest\WebView2` och profilen `HtmlPreview` innan adapter skapas. Navigation väntar på `AdapterCreated` och sker sedan med `NavigateToString`. En seriell operationsspärr, inaktiverade knappar, tidsgränser och livscykelavbrott skyddar navigation, JavaScript och utskrift. Paketversionens asynkrona initieringsfel går genom dispatcher-undantag; en tillfällig handler hanterar bara WebView-fel under denna kontrolls initiering och tas därefter bort. Andra UI-undantag lämnas orörda.

`AddIn.cs`, `EmbeddedUi.cs` och `PackedNativeLibraries.cs` är oförändrade mot bascommitten `13caf55`. Ribbon och dess fokusöverlämning, separat STA-dispatcher och native-resolver används som tidigare.

## Mallar och paketering

Bygget identifierar `mallar/*.html` med wildcard och bäddar in filerna i UI-assemblyn med resursnamnet `AvaloniaDnaTest.Templates.<filnamn>`. Vid körning används en sorterad lista från assemblyns manifest, aldrig projektkatalogen.

| Resurs | Automatiskt WebView2-test | A4 enligt ursprunglig CSS |
|---|---|---|
| inventering.html | PASS | Stående |
| matlador.html | PASS | Liggande |
| patientmeny.html | PASS | Liggande |
| recept.html | PASS | Stående; även namngiven etikettsida |
| svinnlista.html | PASS | Stående |

Filernas ursprungliga bytes och format har inte ändrats. Samtliga sektioner, platshållare och exempelblock skickas till webbläsaren. Platshållare syns därför tillsammans med exempeldata; detta är avsiktligt och ingen mallmotor har implementerats.

Endast HTML-strängen i minnet får ett inledande skript i `head`. Det spärrar `window.print`, samlar JavaScript-fel och signalerar redo efter `load`, `document.fonts.ready` och två animation frames. Mallarnas egna skript får köras som vanligt. Ingen `data-autoprint` läggs till. Testet provar dessutom ett uttryckligt `window.print()` och verifierar att spärren fångar det.

Excel-DNA packar UI-assemblyn och `Avalonia.Controls.WebView.dll` från deps.json. WebView-paketet använder en managed loader för installerad WebView2. Inga nya native-payloads behövdes och ingen ny native-resolver infördes. Slutfilens enda tre native-payloads är fortfarande SkiaSharp, HarfBuzz och ANGLE; WebView2:s runtime-filer kommer från den installerade runtime-miljön. Browserplattformens `av-webview.mjs` används inte på Windows.

`scripts/verify-webview.py` dekomprimerar den verkliga XLL-filens PE-resurser, jämför UI- och WebView-assemblybytes, kontrollerar alla fem exakta HTML-payloads och resursnamn samt återanvänder befintlig ikon/native-verifiering. Det finns ingen fallback till externa HTML-filer.

## Utskrift och PDF

**Skriv ut** väntar tills dokumentets load-skript, typsnitt och layout är redo och anropar `NativeWebView.ShowPrintUI()`. Windows-adaptern öppnar WebView2:s inbyggda utskriftsförhandsgranskning, som använder dokumentets utskrifts-CSS. Automatisk JavaScript-utskrift är fortsatt spärrad. Status och fullständiga undantagskedjor loggas. API-anropet rapporterar inte om användaren senare avbryter eller om skrivaren faktiskt skriver ut.

En egen PDF-knapp levereras **inte**. `PrintToPdfStreamAsync` provades på alla fem mallar och gav giltiga PDF-filer, men mätning med pypdf visade Letter: 612 × 792 punkter respektive 792 × 612 för liggande. A4 i `@page` räckte alltså inte med denna API-väg. Den offentliga `WebViewPrintSettings`-typen har orientering, skalning och marginaler men saknar pappersbredd/höjd och bakgrundsval. En korrekt egen A4-export skulle kräva mer plattformsspecifik integration. Prototypen togs bort för att prioritera stabilitet. Använd vid behov dialogens PDF-destination och välj A4; detta ska verifieras på klienten.

Vid utskrift: välj **A4**, rätt orientering, önskade marginaler, stäng av webbläsarens sidhuvud/sidfot om de inte önskas och aktivera **Bakgrundsgrafik** när färger ska följa med. Kontrollera alltid förhandsvisningen. Fysiskt papper och skrivarens drivrutin är ännu inte verifierade.

## Diagnostik och livscykel

Logg: `%TEMP%\AvaloniaDnaTest\diagnostics.log`. Nya rader beskriver runtime-tillgänglighet/version, profilkatalog, environment/adapter-initiering, vald mall, navigation start/slut, JavaScript-fel, utskriftsförsök och adapterstängning. Data-URI:ns hela HTML-innehåll loggas inte. JavaScript-fångsten täcker `error` och `unhandledrejection`; den är inte en fullständig DevTools-konsollogg.

Vid sidbyte behålls kontrollen i samma visuella träd. När fönstret stängs avbryts väntande operationer och webbkontrollen kopplas bort, vilket får paketet att stänga native-adaptern/controller. `AdapterDestroyed` observerades i Excel. Profil/cache behålls mellan öppningar; inga andra programs data rensas. Processkontroll efter fönsterstängning hittade inga `msedgewebview2.exe` vars kommandorad innehöll `AvaloniaDnaTest`, trots att Excel fortfarande var igång. Andra programs WebView2-processer lämnades orörda.

## Automatiskt verifierat

- Låst paketåterställning: PASS. Release-bygge och packning: **0 fel, 0 varningar**.
- Tidigare host-smoke: fönsteråteranvändning, tre öppna/stäng-cykler, stängning mitt i fokusöverlämning och återöppning, TextBox-bindning i båda riktningar, Slider/ProgressBar, tio DataGrid-rader, ljust/mörkt tema och asynkron informationsservice: PASS.
- Riktig NativeWebView/WebView2 i testvärden: samtliga fem mallar, navigation, minst en tabell/stilmall/A4-page-regel, synlig dokumenttext, noll JavaScript-fel, noll externa resursanrop och noll automatisk utskrift: PASS. Patientmenyns receptkodsmarkering verifierades i DOM.
- Omladdning, sidbyte samt stängning/återöppning av NativeWebView på samma STA-runtime: PASS.
- Packade HTML-bytes, manifestnamn, managed WebView-assembly, tre Ribbon-ikoner och tre befintliga native-payloads: PASS.
- Isolerad Skia/HarfBuzz-resolverregression och upprepad resolverregistrering: PASS.

Testloggar finns i `artifacts/webview-build.log`, `webview-controls.log`, `webview-smoke.log`, `webview-payloads.txt`; DOM-resultaten finns i `artifacts/webview/*.html.json`. `artifacts` är lokal testutdata och ingår inte i distributionen eller Git.

## Visuellt verifierat i Excel och begränsningar

Packad XLL laddades från `dist` i en ny Excel-instans. Loading observerades, sedan grön Open medan Avalonia hade fokus. Inventeringsmallen visades med tabell, svenska tecken, CSS och exempeldata. Alla fem alternativ syntes i mallväljaren. **Skriv ut** öppnade WebView2:s svenska dialog med förhandsvisad tabell och val för stående/liggande. Ingen utskrift skickades till skrivaren. Fönsterstängning gav `AdapterDestroyed` och Ribbon Ready.

Computer-use kan styra det ägda Avalonia-fönstrets vanliga knappar via Excel men tappar målfönster/fokus för popupval och den separata WebView2-processen. Därför är inte byte genom alla fem mallval eller inställningarna inne i utskriftsdialogen visuellt accepterade i Excel. Hostens automatiska tester täcker mallbytena.

En COM-styrd testomgång lämnade en dold Excel-process efter `Quit`; testboken och WebView-processerna var redan stängda. Den egna testprocessen avslutades för att frigöra XLL-filen. Testhjälpen har förbättrats till explicita COM-referenser och städning samt tolererar att Excel redan har stängts.

**Slutversionen testades därefter med vanlig Excel-stängning via krysset, medan HTML-fönstret var öppet.** Efter att den egna osparade testboken avfärdats försvann både Excel-processen och alla WebView2-processer för appens profil utan tvångsavslutning. En separat `AutoClose complete`-loggrad kunde inte bekräftas. Detta verifierar processavslutning i en omgång, inte alla kombinationer av utskriftsdialog och väntande informationsanrop. COM-hjälpen rapporterade då att testboken redan var frånkopplad; den nya catch-hanteringen infördes medan just den hjälpomgången redan körde och gäller efterföljande starter.

## Reproducera

```powershell
./scripts/Build.ps1 -WebSmoke
python scripts/verify-webview.py
./scripts/Test-Native.ps1
./scripts/Test-Excel.ps1
```

Vid behov: ange en fungerande NuGet-konfiguration med `-NuGetConfig`. Paketversionerna är låsta i alla tre `packages.lock.json`. För lokal offline-återställning med redan hämtade paket kan `dotnet restore AvaloniaDnaTest.sln --locked-mode --source .packages -p:NuGetAudit=false` användas, med `NUGET_PACKAGES` satt till projektets `.packages`. Testvärden är en utvecklarhjälp och ska inte distribueras.

## Testprotokoll på jobbdatorn

Kopiera endast XLL-filen till en tom användarkatalog. Prova även med projektkatalogen och `mallar` helt otillgängliga.

| Test | Förväntat | Klientresultat |
|---|---|---|
| Första Ribbon-klick i ny Excel-process | Ready → inaktiverad Loading → Open före fokusöverlämning | Ej kört |
| Alla fem mallar, byt snabbt och uppdatera | Exempeldata/CSS/svenska tecken syns; rätt dokument; inga externa filer | Ej kört |
| Patientmeny och matlådor | Kodmarkering, textanpassning, tabeller och liggande layout | Ej kört |
| Recept och inventering/svinnlista | Stående layout, etiketter och sidbrytningar; granska alla sidor | Ej kört |
| Öppna mall utan utskriftskommando | Ingen utskriftsdialog | Ej kört |
| Skriv ut, A4 stående och liggande | @page/@media print, marginaler och sidbrytningar följs | Ej kört |
| Bakgrundsgrafik av/på | Färger och kontrast blir rätt; sidhuvud/sidfot enligt önskemål | Ej kört |
| PDF via dialogens PDF-destination | Verklig A4-storlek och korrekt färg/sidantal i sparad PDF | Ej kört |
| Fysisk skrivare | A4, orientering, skalning, tabeller och sidbrytningar korrekta på papper | Ej kört |
| Fäll ihop menyn, byt sidor/tema | Dokumentytan fungerar; alla tidigare UI-kontroller kvar | Ej kört |
| Öppet/minimerat fönster, tio öppna/stäng-cykler | Samma fönster aktiveras; återöppning fungerar; ingen processtillväxt | Ej kört |
| Saknad runtime i separat testmiljö | Begriplig status; övriga UI-sidor fungerar | Ej kört |
| Initieringsfel eller ej skrivbar egen profil | Loggad felkedja och responsivt Excel; starta om efter runtime-fel | Ej kört |
| Vanlig Excel-avslutning med HTML/utskrift/informationsanrop öppna | Ingen kvarvarande egen Excel-/WebView2-process | Ej kört |

## Primärkällor

- [Avalonia NativeWebView API och utskrift](https://docs.avaloniaui.net/controls/web/nativewebview).
- [WebView2 environment och egen användardatakatalog](https://docs.avaloniaui.net/controls/web/webview-environment).
- [Exakt paketkällkod, commit 43d26d9](https://github.com/AvaloniaUI/Avalonia.Controls.WebView/tree/43d26d90312b338d1d82bd12b215c16b5f3fa195), särskilt `ManagedWebView2Loader.cs`, `WebView2BaseAdapter.cs`, `NativeWebViewControlHost.cs` och `WebViewPrintSettings.cs`. Installerad nuspec och lockfiler verifierar de använda versionerna.
