using AvaloniaDnaTest.UI;
using System.Text.Json;

internal static class WebSmoke
{
    public static async Task RunAsync(EmbeddedUi ui)
    {
        Directory.CreateDirectory("artifacts/webview");
        if (HtmlTemplates.Names.Count != 5) throw new Exception("Expected five discovered embedded HTML resources.");
        await ui.ShowAsync(0);
        HtmlPreview preview = null!;
        await ui.InspectWindowAsync(w => preview = w.OpenHtmlPreview());
        await preview.InitialLoad.WaitAsync(TimeSpan.FromSeconds(50));
        foreach (string name in HtmlTemplates.Names)
        {
            Task operation = Task.CompletedTask;
            await ui.InspectWindowAsync(_ => operation = preview.LoadTemplateAsync(name));
            await operation;
            string? result = null;
            await ui.InspectWindowAsync(_ => operation = ReadAsync());
            await operation;
            async Task ReadAsync() => result = await preview.InspectAsync("""
                JSON.stringify({ready:window.__previewReady, errors:window.__previewErrors,
                  printCalls:window.__previewPrintCalls, text:document.body.innerText.length,
                  tables:document.querySelectorAll('table').length,
                  styles:document.styleSheets.length, scripts:document.scripts.length,
                  codes:document.querySelectorAll('.cell-inner .kod').length,
                  rowHeight:getComputedStyle(document.documentElement).getPropertyValue('--rad-h'),
                  external:performance.getEntriesByType('resource').map(x=>x.name),
                  pageRules:Array.from(document.styleSheets).flatMap(s=>Array.from(s.cssRules))
                    .filter(r=>r.type===CSSRule.PAGE_RULE).map(r=>r.cssText)})
                """);
            string json = JsonSerializer.Deserialize<string>(result!)!;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.GetProperty("ready").GetBoolean() || root.GetProperty("errors").GetArrayLength() != 0
                || root.GetProperty("printCalls").GetInt32() != 0 || root.GetProperty("text").GetInt32() < 100
                || root.GetProperty("tables").GetInt32() < 1 || root.GetProperty("styles").GetInt32() < 1
                || root.GetProperty("external").GetArrayLength() != 0 || root.GetProperty("pageRules").GetArrayLength() < 1)
                throw new Exception("HTML DOM validation failed: " + name + " " + json);
            if (name == "patientmeny.html" && root.GetProperty("codes").GetInt32() == 0)
                throw new Exception("Patient menu JavaScript did not mark recipe codes.");
            if (name == "matlador.html" && root.GetProperty("rowHeight").GetString() == "")
                throw new Exception("Meal box CSS row height is missing.");
            File.WriteAllText("artifacts/webview/" + name + ".json", json);
            await ui.InspectWindowAsync(_ => operation = VerifyPrintGuardAsync());
            await operation;
            async Task VerifyPrintGuardAsync()
            {
                if (await preview.InspectAsync("window.print(); window.__previewPrintCalls === 1") != "true")
                    throw new Exception("Preview auto-print guard failed: " + name);
            }
            Console.WriteLine("PASS: " + name + "; DOM/CSS/examples/JS/no auto-print/no external resources");
        }
        // Reload and navigate away/back while retaining the same native control.
        Task reload = Task.CompletedTask;
        await ui.InspectWindowAsync(w => { w.ShowPage("ControlsPage"); w.OpenHtmlPreview(); reload = preview.LoadTemplateAsync(HtmlTemplates.Names[0]); });
        await reload;
        await ui.CloseWindowAsync();
        await ui.ShowAsync(0);
        await ui.InspectWindowAsync(w => preview = w.OpenHtmlPreview());
        await preview.InitialLoad.WaitAsync(TimeSpan.FromSeconds(50));
        await ui.CloseWindowAsync();
        Console.WriteLine("PASS: reload; page switching; close and reopen NativeWebView on the same STA runtime.");
    }
}
