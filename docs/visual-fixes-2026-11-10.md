# Visuella korrigeringar 2026-11-10

Gren: feature/native-webview.

## Orsak och ändring

- ControlsPage använde ScrollViewerns automatiskt döljande Fluent-scrollbar som kan överlagra innehållet. AllowAutoHide=False låter standardmallen reservera scrollbarens faktiska bredd. VerticalScrollBarVisibility=Auto och HorizontalScrollBarVisibility=Disabled behåller vertikal scrollning och breddanpassning utan extra marginaler. När innehållet ryms återfår det hela bredden.
- Sidomenyn hade hårdkodad mörk bakgrund (#152E50) och rubrikfärg (#93C5FD), medan knapparnas text följde aktivt tema. Dessa ersattes med Navigation*-penslar i App.axamls ljusa och mörka ThemeDictionaries och DynamicResource-bindningar. Gemensamma stilar hanterar normal-, hover-, pressed- och selected-lägen. ShowPage uppdaterar selected-klassen även vid programmatisk navigation. Semantiska resursnamn möjliggör framtida lokala accentöverskrivningar utan nytt temasystem.
- Sidomenyn innehåller inga separata ikoner. Menyknappens befintliga hamburgersymbol följer fortfarande Fluent-temat.

## Genomförda tester

- Visuellt granskade RenderTargetBitmap-bilder från den riktiga Avalonia-testvärden, i både ljust och mörkt tema: 900x600, 1100x750, 1280x900 och 1280x1100 (layoutenheter). Bilder: artifacts/visual-<bredd>x<höjd>-<dark|light>.png.
- Testvärden ändrade samma fönsters storlek och tema mellan fallen. Automatiska geometrikontroller verifierade scrollbarens högra kant och att innehållets högra kant inte överlappar ScrollViewerns egen scrollbar. Vid de tre mindre storlekarna reserveras 16 layoutenheter; vid 1280x1100 försvinner scrollbaren och innehållet använder hela bredden. Programmatisk scrollning verifierades. Logg: artifacts/visual-fix-layout.log. Återkörning: Host.dll --visual-smoke.
- Interaktivt via computer-use: navigation, mushjul nedåt och dragning av scrollbarens handtag uppåt i mörkt tema vid 1280x900; direkt växling från mörkt till ljust med läsbar sidomeny och bibehållen sidmarkering. Mushjul/dragning provades inte separat i ljust tema. Hover granskades i mörkt tema; pressed och tangentbordsfokus granskades inte separat.
- Befintligt --smoke passerade: bindningar, båda teman, asynkron information, återanvändning/stängning/återöppning och stängning under aktivering.
- Befintligt --web-smoke passerade: samtliga fem HTML-mallar, DOM/CSS/exempel/JS, ingen automatisk utskrift eller externa resurser, omladdning, sidbyte och stängning/återöppning av NativeWebView på samma STA-runtime.
- Release-byggnad: 0 varningar och 0 fel. dist/AvaloniaDnaTest-x64.xll uppdaterad. Inspect-Xll verifierade AMD64 och inbäddade managed assemblies samt tre native-bibliotek. Bygglogg: artifacts/visual-fix-build.log; resurser: artifacts/visual-fix-packed-resources.txt; SHA256: artifacts/xll-sha256.txt.

## Avgränsning

Visuella tester gjordes i den separata testvärden med samma EmbeddedUi/STA-bootstrap. Den nya XLL-filen laddades inte i Excel i denna omgång. Ribbon, faktisk utskrift och native-resolverns extraktion från den nya XLL-filen har därför inte acceptanstestats på nytt. Ingen produktionslogik för dessa delar ändrades. Tidigare arbetsändringar i grenen behölls.
